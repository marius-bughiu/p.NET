using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using PNet.Generators.Model;

namespace PNet.Generators.Metadata;

/// <summary>
/// Walks runtime impl DLLs with PEReader to enumerate every non-public member.
/// Uses the consumer's Compilation to validate that every referenced type is
/// actually present in the ref-pack the end consumer will compile against —
/// if not, the member is skipped so the generated library stays compilable.
/// </summary>
internal static class PeScanner
{
    public static IReadOnlyList<TypeModel> Scan(
        IReadOnlyList<string> runtimeImplPaths,
        ISet<string>? targetNamespaces,
        Compilation consumerCompilation)
    {
        var results = new List<TypeModel>();

        foreach (var path in runtimeImplPaths)
        {
            try
            {
                ScanAssembly(path, targetNamespaces, consumerCompilation, results);
            }
            catch
            {
                // Skip unreadable / invalid assemblies.
            }
        }

        return results;
    }

    private static void ScanAssembly(string path, ISet<string>? targetNamespaces, Compilation consumer, List<TypeModel> results)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return;
        var reader = pe.GetMetadataReader();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (type.IsNested) continue;

            var ns = type.Namespace.IsNil ? "" : reader.GetString(type.Namespace);
            if (string.IsNullOrEmpty(ns)) continue; // skip the global namespace
            if (targetNamespaces is not null && !targetNamespaces.Contains(ns)) continue;

            var rawName = reader.GetString(type.Name);
            if (rawName.Length == 0 || rawName[0] == '<') continue;

            var attrs = type.Attributes;
            var visibility = attrs & TypeAttributes.VisibilityMask;
            if (visibility != TypeAttributes.Public) continue;

            // Skip compiler-generated and ref-struct types.
            if (IsCompilerGenerated(reader, type.GetCustomAttributes())) continue;
            if (IsByRefLike(reader, type.GetCustomAttributes())) continue;

            // Skip static classes — the emitter doesn't generate extension blocks for them
            // (C# 14 static-extension shapes are out of scope for v1), so they would produce
            // empty .g.cs files that just create a phantom namespace.
            bool isStaticClass = (attrs & TypeAttributes.Abstract) != 0 && (attrs & TypeAttributes.Sealed) != 0;
            if (isStaticClass) continue;

            var simpleName = StripArity(rawName);
            if (!consumer.TypeExistsByMetadataName(ns, rawName)) continue;

            var typeParamNames = new List<string>();
            var typeParamConstraints = new List<string>();
            bool hasUnsupportedConstraints = false;
            foreach (var gp in type.GetGenericParameters())
            {
                var gParam = reader.GetGenericParameter(gp);
                typeParamNames.Add(reader.GetString(gParam.Name));
                typeParamConstraints.Add(BuildConstraintClause(reader, gParam));
                // Type-level constraints (where T : IEquatable<T>, where T : SomeBaseClass) are hard to
                // reproduce because they can reference generic parameters of the surrounding type. Skip
                // the type entirely when any are present — better than emitting code that won't compile.
                if (gParam.GetConstraints().Count > 0) hasUnsupportedConstraints = true;
            }
            if (hasUnsupportedConstraints) continue;

            var typeGenericContext = new GenericContext(typeParamNames, new string[0]);

            var members = new List<MemberModel>();
            var seenWrapperNames = new HashSet<string>(StringComparer.Ordinal);

            // ---- fields ----
            foreach (var fh in type.GetFields())
            {
                var field = reader.GetFieldDefinition(fh);
                var fAttrs = field.Attributes;
                if ((fAttrs & FieldAttributes.Literal) != 0) continue;
                if ((fAttrs & FieldAttributes.SpecialName) != 0) continue;
                var access = fAttrs & FieldAttributes.FieldAccessMask;
                if (access == FieldAttributes.Public) continue; // only non-public
                if (access == FieldAttributes.PrivateScope) continue;

                var name = reader.GetString(field.Name);
                if (!IsValidIdentifier(name)) continue;

                var provider = new CSharpSignatureProvider();
                var typeSig = field.DecodeSignature(provider, typeGenericContext);
                if (provider.HasPointer || provider.HasFunctionPointer || provider.HasUnresolvableNested) continue;
                if (!AllReferencesVisible(provider, consumer)) continue;

                bool isStatic = (fAttrs & FieldAttributes.Static) != 0;
                var wrapper = Unique(seenWrapperNames, WrapperName(name));

                members.Add(new MemberModel(
                    Kind: isStatic ? MemberKind.StaticField : MemberKind.InstanceField,
                    Name: name,
                    WrapperName: wrapper,
                    ReturnTypeSignature: typeSig,
                    ReturnsVoid: false,
                    ReturnsByRef: false,
                    ReturnsByRefReadonly: (fAttrs & FieldAttributes.InitOnly) != 0,
                    Parameters: Array.Empty<ParameterModel>(),
                    MethodTypeParameters: Array.Empty<string>()));
            }

            // ---- methods ----
            foreach (var mh in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(mh);
                var mAttrs = method.Attributes;
                if ((mAttrs & MethodAttributes.SpecialName) != 0) continue;
                if ((mAttrs & MethodAttributes.RTSpecialName) != 0) continue;
                var access = mAttrs & MethodAttributes.MemberAccessMask;
                if (access == MethodAttributes.Public) continue;
                if (access == MethodAttributes.PrivateScope) continue;

                var name = reader.GetString(method.Name);
                if (!IsValidIdentifier(name)) continue;

                if (method.GetGenericParameters().Count > 0) continue; // v1: skip generic methods

                var provider = new CSharpSignatureProvider();
                var signature = method.DecodeSignature(provider, typeGenericContext);
                if (provider.HasPointer || provider.HasFunctionPointer || provider.HasUnresolvableNested) continue;
                if (!AllReferencesVisible(provider, consumer)) continue;

                var paramNames = new List<string>();
                foreach (var pHandle in method.GetParameters())
                {
                    var p = reader.GetParameter(pHandle);
                    if (p.SequenceNumber == 0) continue;
                    paramNames.Add(reader.GetString(p.Name));
                }

                var parameters = new List<ParameterModel>();
                for (int i = 0; i < signature.ParameterTypes.Length; i++)
                {
                    var pSig = signature.ParameterTypes[i];
                    var pName = i < paramNames.Count ? paramNames[i] : "arg" + i;
                    if (!IsValidIdentifier(pName)) pName = "arg" + i;

                    var refKind = ParamRefKind.None;
                    if (pSig.StartsWith("ref ", StringComparison.Ordinal))
                    {
                        pSig = pSig.Substring(4);
                        refKind = ParamRefKind.Ref;
                    }

                    parameters.Add(new ParameterModel(pName, pSig, refKind));
                }

                var returnType = signature.ReturnType;
                bool returnsByRef = returnType.StartsWith("ref ", StringComparison.Ordinal);
                if (returnsByRef) returnType = returnType.Substring(4);
                bool returnsVoid = returnType == "void";

                bool isStatic = (mAttrs & MethodAttributes.Static) != 0;
                var wrapper = Unique(seenWrapperNames, WrapperName(name));

                members.Add(new MemberModel(
                    Kind: isStatic ? MemberKind.StaticMethod : MemberKind.InstanceMethod,
                    Name: name,
                    WrapperName: wrapper,
                    ReturnTypeSignature: returnType,
                    ReturnsVoid: returnsVoid,
                    ReturnsByRef: returnsByRef,
                    ReturnsByRefReadonly: false,
                    Parameters: parameters,
                    MethodTypeParameters: Array.Empty<string>()));
            }

            if (members.Count == 0) continue;

            results.Add(new TypeModel(
                Namespace: ns,
                TypeName: simpleName,
                ArityCount: typeParamNames.Count,
                TypeParameterNames: typeParamNames,
                TypeParameterConstraints: typeParamConstraints,
                IsValueType: IsValueTypeDef(reader, type),
                IsStatic: (attrs & TypeAttributes.Abstract) != 0 && (attrs & TypeAttributes.Sealed) != 0,
                IsSealed: (attrs & TypeAttributes.Sealed) != 0,
                IsGenericDefinition: typeParamNames.Count > 0,
                Members: members));
        }
    }

    private static bool AllReferencesVisible(CSharpSignatureProvider provider, Compilation consumer)
    {
        foreach (var fullName in provider.TopLevelTypeReferences)
        {
            if (consumer.GetTypeByMetadataName(fullName) is null) return false;
        }
        return true;
    }

    private static bool IsCompilerGenerated(MetadataReader reader, CustomAttributeHandleCollection handles)
        => HasAttribute(reader, handles, "CompilerGeneratedAttribute");

    private static bool IsByRefLike(MetadataReader reader, CustomAttributeHandleCollection handles)
        => HasAttribute(reader, handles, "IsByRefLikeAttribute");

    private static bool HasAttribute(MetadataReader reader, CustomAttributeHandleCollection handles, string attributeName)
    {
        foreach (var h in handles)
        {
            var attr = reader.GetCustomAttribute(h);
            switch (attr.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    {
                        var mref = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                        if (mref.Parent.Kind == HandleKind.TypeReference)
                        {
                            var tref = reader.GetTypeReference((TypeReferenceHandle)mref.Parent);
                            if (reader.GetString(tref.Name) == attributeName) return true;
                        }
                        else if (mref.Parent.Kind == HandleKind.TypeDefinition)
                        {
                            var tdef = reader.GetTypeDefinition((TypeDefinitionHandle)mref.Parent);
                            if (reader.GetString(tdef.Name) == attributeName) return true;
                        }
                        break;
                    }
                case HandleKind.MethodDefinition:
                    {
                        // Same-assembly attribute (e.g. IsByRefLikeAttribute defined inside CoreLib itself).
                        var mdef = reader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor);
                        var owner = reader.GetTypeDefinition(mdef.GetDeclaringType());
                        if (reader.GetString(owner.Name) == attributeName) return true;
                        break;
                    }
            }
        }
        return false;
    }

    private static bool IsValueTypeDef(MetadataReader reader, TypeDefinition type)
    {
        var bt = type.BaseType;
        if (bt.IsNil || bt.Kind != HandleKind.TypeReference) return false;
        var tr = reader.GetTypeReference((TypeReferenceHandle)bt);
        var ns = tr.Namespace.IsNil ? "" : reader.GetString(tr.Namespace);
        var nm = reader.GetString(tr.Name);
        return ns == "System" && (nm == "ValueType" || nm == "Enum");
    }

    private static string StripArity(string name)
    {
        int idx = name.IndexOf('`');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }

    private static bool IsValidIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (!(char.IsLetter(s[0]) || s[0] == '_')) return false;
        for (int i = 1; i < s.Length; i++)
            if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_')) return false;
        return true;
    }

    private static string BuildConstraintClause(MetadataReader reader, GenericParameter gp)
    {
        var parts = new List<string>();
        var attrs = gp.Attributes;

        // Variance flags live in the high nibble; filter to just constraint bits.
        if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0) parts.Add("class");
        if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) parts.Add("struct");

        // `notnull` is encoded via a NullableAttribute(1) on the generic parameter.
        bool isNotNull = false;
        foreach (var attrHandle in gp.GetCustomAttributes())
        {
            var attr = reader.GetCustomAttribute(attrHandle);
            if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
            var mref = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            if (mref.Parent.Kind != HandleKind.TypeReference) continue;
            var tref = reader.GetTypeReference((TypeReferenceHandle)mref.Parent);
            if (reader.GetString(tref.Name) == "NullableAttribute")
            {
                var blob = attr.DecodeValue(new SimpleAttributeDecoder(reader));
                foreach (var fa in blob.FixedArguments)
                {
                    if (fa.Value is byte b && b == 1) isNotNull = true;
                }
            }
        }
        if (isNotNull && !parts.Contains("class")) parts.Add("notnull");

        if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0
            && !parts.Contains("struct")) parts.Add("new()");

        if (parts.Count == 0) return "";
        return "where " + reader.GetString(gp.Name) + " : " + string.Join(", ", parts);
    }

    public static string WrapperName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "p_";
        var trimmed = name;
        if (trimmed[0] == '_' && trimmed.Length > 1) trimmed = trimmed.Substring(1);
        return "p_" + trimmed;
    }

    private static string Unique(HashSet<string> seen, string candidate)
    {
        if (seen.Add(candidate)) return candidate;
        for (int i = 1; ; i++)
        {
            var next = candidate + "_" + i;
            if (seen.Add(next)) return next;
        }
    }
}

internal static class CompilationExtensions
{
    public static bool TypeExistsByMetadataName(this Compilation consumer, string ns, string rawName)
    {
        var full = string.IsNullOrEmpty(ns) ? rawName : ns + "." + rawName;
        return consumer.GetTypeByMetadataName(full) is not null;
    }
}
