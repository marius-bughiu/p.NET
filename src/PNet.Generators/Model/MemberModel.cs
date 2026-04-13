using System;
using System.Collections.Generic;

namespace PNet.Generators.Model;

internal enum MemberKind
{
    InstanceField,
    StaticField,
    InstanceMethod,
    StaticMethod,
}

internal sealed record MemberModel(
    MemberKind Kind,
    string Name,                 // metadata name (e.g. "_items", "EnsureCapacity")
    string WrapperName,          // computed ext name (e.g. "p_items")
    string ReturnTypeSignature,  // C# source form, already-qualified with global::
    bool ReturnsVoid,
    bool ReturnsByRef,
    bool ReturnsByRefReadonly,
    IReadOnlyList<ParameterModel> Parameters,
    IReadOnlyList<string> MethodTypeParameters);

internal sealed record ParameterModel(
    string Name,
    string TypeSignature,
    ParamRefKind RefKind);

internal enum ParamRefKind
{
    None,
    In,
    Out,
    Ref,
    RefReadonly,
}

internal sealed record TypeModel(
    string Namespace,
    string TypeName,
    int ArityCount,
    IReadOnlyList<string> TypeParameterNames,
    IReadOnlyList<string> TypeParameterConstraints, // one constraint-clause-suffix per type param, e.g. ": notnull" (can be empty string for none)
    bool IsValueType,
    bool IsStatic,
    bool IsSealed,
    bool IsGenericDefinition,
    IReadOnlyList<MemberModel> Members);
