using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace PNet.Generators.Metadata;

/// <summary>
/// Minimal custom-attribute blob decoder — we only need enough to peek at a single byte or a byte-array
/// argument (for <c>NullableAttribute</c> detection).
/// </summary>
internal sealed class SimpleAttributeDecoder : ICustomAttributeTypeProvider<string>
{
    private readonly MetadataReader _reader;

    public SimpleAttributeDecoder(MetadataReader reader) { _reader = reader; }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSystemType() => "System.Type";
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => "";
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => "";
    public string GetTypeFromSerializedName(string name) => name;
    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
    public bool IsSystemType(string type) => type == "System.Type";
}
