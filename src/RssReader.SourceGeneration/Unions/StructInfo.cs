namespace RssReader.SourceGeneration.Unions;

internal record StructInfo(
    string Namespace,
    string Name,
    bool IsGeneric,
    bool GenerateJsonConverter,
    TypeInfo[] Types);
