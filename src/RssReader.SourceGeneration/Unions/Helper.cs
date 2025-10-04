namespace RssReader.SourceGeneration.Unions;

internal static class Helper
{
    public static string GetGenericSuffix(string[] typeNames)
        => typeNames.Length > 0
            ? $"<{string.Join(", ", typeNames)}>"
            : "";

    public static string GetGenericSuffix(this StructInfo structInfo)
        => structInfo.IsGeneric
            ? $"<{string.Join(", ", structInfo.Types.Where(t => t.IsGenericParameter).Select(t => t.TypeName))}>"
            : "";

    public static string GetOpenGenericSuffix(this StructInfo structInfo)
        => structInfo.IsGeneric
            ? $"<{new string(',', structInfo.Types.Where(t => t.IsGenericParameter).Count() - 1)}>"
            : "";

    public static string GetFullTypeName(this StructInfo structInfo)
        => structInfo.IsGeneric
            ? $"{structInfo.Name}{GetGenericSuffix(structInfo.Types.Where(t => t.IsGenericParameter).Select(t => t.TypeName).ToArray())}"
            : structInfo.Name;

    public static string GetNormalizedTypeName(this StructInfo structInfo)
        => structInfo.IsGeneric
            ? $"{structInfo.Name}_{string.Join("_", structInfo.Types.Where(t => t.IsGenericParameter).Select(t => t.TypeName))}"
            : structInfo.Name;

    public static string GetTypeAccessor(this TypeInfo typeInfo)
        => $"_t{typeInfo.Index}{(typeInfo.IsValueType ? "!.Value" : "!")}";
}
