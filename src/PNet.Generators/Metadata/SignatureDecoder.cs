using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace PNet.Generators.Metadata;

/// <summary>
/// Decodes metadata signature blobs into C# source-form type strings.
/// Tracks whether the signature references any type the consuming compilation
/// cannot see (internal-to-CoreLib, stripped from ref pack, etc.) so we can skip it.
/// </summary>
internal sealed class CSharpSignatureProvider : ISignatureTypeProvider<string, GenericContext>
{
    // Collected reference assembly-qualified top-level type names encountered during decoding.
    public HashSet<string> TopLevelTypeReferences { get; } = new();

    public bool HasPointer { get; private set; }
    public bool HasFunctionPointer { get; private set; }
    public bool HasUnresolvableNested { get; private set; }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "global::System.Boolean",
        PrimitiveTypeCode.Byte => "global::System.Byte",
        PrimitiveTypeCode.SByte => "global::System.SByte",
        PrimitiveTypeCode.Char => "global::System.Char",
        PrimitiveTypeCode.Int16 => "global::System.Int16",
        PrimitiveTypeCode.UInt16 => "global::System.UInt16",
        PrimitiveTypeCode.Int32 => "global::System.Int32",
        PrimitiveTypeCode.UInt32 => "global::System.UInt32",
        PrimitiveTypeCode.Int64 => "global::System.Int64",
        PrimitiveTypeCode.UInt64 => "global::System.UInt64",
        PrimitiveTypeCode.Single => "global::System.Single",
        PrimitiveTypeCode.Double => "global::System.Double",
        PrimitiveTypeCode.IntPtr => "global::System.IntPtr",
        PrimitiveTypeCode.UIntPtr => "global::System.UIntPtr",
        PrimitiveTypeCode.String => "global::System.String",
        PrimitiveTypeCode.Object => "global::System.Object",
        PrimitiveTypeCode.Void => "void",
        PrimitiveTypeCode.TypedReference => "global::System.TypedReference",
        _ => "object",
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var def = reader.GetTypeDefinition(handle);
        var name = reader.GetString(def.Name);
        if (def.IsNested)
        {
            var outer = GetTypeFromDefinition(reader, def.GetDeclaringType(), rawTypeKind);
            // Nested generic types are hard to form correctly — outer may need type args that
            // we don't have here. Flag as unresolvable; caller will skip the member.
            HasUnresolvableNested = true;
            return outer + "." + StripArity(name);
        }

        var ns = def.Namespace.IsNil ? "" : reader.GetString(def.Namespace);
        var topLevel = (string.IsNullOrEmpty(ns) ? name : ns + "." + name);
        TopLevelTypeReferences.Add(topLevel);
        return "global::" + (string.IsNullOrEmpty(ns) ? StripArity(name) : ns + "." + StripArity(name));
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var name = reader.GetString(tr.Name);
        var ns = tr.Namespace.IsNil ? "" : reader.GetString(tr.Namespace);

        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var outer = GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, rawTypeKind);
            HasUnresolvableNested = true;
            return outer + "." + StripArity(name);
        }
        if (tr.ResolutionScope.Kind == HandleKind.TypeDefinition)
        {
            var outer = GetTypeFromDefinition(reader, (TypeDefinitionHandle)tr.ResolutionScope, rawTypeKind);
            HasUnresolvableNested = true;
            return outer + "." + StripArity(name);
        }

        if (!string.IsNullOrEmpty(name) && name[0] == '<') HasUnresolvableNested = true;

        var topLevel = (string.IsNullOrEmpty(ns) ? name : ns + "." + name);
        TopLevelTypeReferences.Add(topLevel);
        return "global::" + (string.IsNullOrEmpty(ns) ? StripArity(name) : ns + "." + StripArity(name));
    }

    public string GetTypeFromSpecification(MetadataReader reader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var spec = reader.GetTypeSpecification(handle);
        return spec.DecodeSignature(this, genericContext);
    }

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        var sb = new StringBuilder(elementType);
        sb.Append('[');
        for (int i = 0; i < shape.Rank - 1; i++) sb.Append(',');
        sb.Append(']');
        return sb.ToString();
    }

    public string GetByReferenceType(string elementType) => "ref " + elementType;

    public string GetPointerType(string elementType)
    {
        HasPointer = true;
        return elementType + "*";
    }

    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        HasFunctionPointer = true;
        return "global::System.IntPtr";
    }

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var sb = new StringBuilder();
        sb.Append(genericType);
        sb.Append('<');
        for (int i = 0; i < typeArguments.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(typeArguments[i]);
        }
        sb.Append('>');
        return sb.ToString();
    }

    public string GetGenericMethodParameter(GenericContext genericContext, int index)
        => index < genericContext.MethodParameters.Count ? genericContext.MethodParameters[index] : "TM" + index;

    public string GetGenericTypeParameter(GenericContext genericContext, int index)
        => index < genericContext.TypeParameters.Count ? genericContext.TypeParameters[index] : "T" + index;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    private static string StripArity(string name)
    {
        int idx = name.IndexOf('`');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }
}

internal sealed class GenericContext
{
    public IReadOnlyList<string> TypeParameters { get; }
    public IReadOnlyList<string> MethodParameters { get; }

    public GenericContext(IReadOnlyList<string> typeParameters, IReadOnlyList<string> methodParameters)
    {
        TypeParameters = typeParameters;
        MethodParameters = methodParameters;
    }

    public static readonly GenericContext Empty = new(new string[0], new string[0]);
}
