using RssReader.SourceGeneration.Unions.Generators;

namespace RssReader.SourceGeneration.Unions;

[Generator]
public sealed class UnionSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        AddUnionGenerator(context, 1);
        AddUnionGenerator(context, 2);
        AddUnionGenerator(context, 3);
        AddUnionGenerator(context, 4);
        AddUnionGenerator(context, 5);
        AddUnionGenerator(context, 6);
    }

    private static void AddUnionGenerator(IncrementalGeneratorInitializationContext context, int arity)
    {
        context.AddGenerateUnionAttribute(arity);

        var valuesProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                $"{Constants.PublicNamespace}.{Constants.AttributeName}`{arity}",
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetStructInfo(ctx));

        context.RegisterSourceOutput(
            valuesProvider,
            static (context, structInfo) =>
            {
                context.AddUnion(structInfo);

                if (structInfo.GenerateJsonConverter)
                {
                    context.AddUnionJsonConverter(structInfo);
                }
            });
    }

    private static StructInfo GetStructInfo(GeneratorAttributeSyntaxContext context)
    {
        var index = 0;

        var attribute = context.Attributes.First();

        var generateJsonConverter = attribute
            .NamedArguments
            .FirstOrDefault(kv => kv.Key == Constants.AttributeGenerateJsonConverterPropertyName)
            .Value.Value is bool boolValue
                ? boolValue
                : false;

        var attributeTypeInfo = attribute.AttributeClass!.TypeArguments
            .Select(a => new TypeInfo(a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), a.IsValueType, false, index++))
            .ToArray();

        var genericTypeInfo = context.TargetSymbol is INamedTypeSymbol namedSymbol && namedSymbol.IsGenericType
            ? namedSymbol.TypeArguments
                .Select(a => new TypeInfo(a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), a.IsValueType, true, index++))
                .ToArray()
            : [];

        return new(
            context.TargetSymbol.ContainingNamespace.ToDisplayString(),
            context.TargetSymbol.Name,
            genericTypeInfo.Length > 0,
            generateJsonConverter,
            [.. attributeTypeInfo, .. genericTypeInfo]);
    }
}
