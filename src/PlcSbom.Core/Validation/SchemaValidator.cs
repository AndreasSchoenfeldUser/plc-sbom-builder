using NJsonSchema;
using NJsonSchema.References;
using NJsonSchema.Validation;

namespace PlcSbom.Core.Validation;

/// <summary>
/// Validates a CycloneDX 1.6 JSON document against the embedded official schema (CycloneDX
/// specification tag 1.6.2). References to spdx.schema.json and jsf-0.82.schema.json are resolved
/// from embedded resources; any other reference fails, so validation never touches the network.
/// </summary>
public static class SchemaValidator
{
    public const string SchemaFile = "bom-1.6.schema.json";
    private const string ResourcePrefix = "PlcSbom.Schemas.";
    private const string VirtualRoot = "/plc-sbom/schemas/";

    private static readonly Lazy<Task<JsonSchema>> Schema = new(() => LoadAsync(SchemaFile));

    /// <summary>Returns the schema violations; empty if the document is valid.</summary>
    public static async Task<IReadOnlyList<SchemaError>> ValidateAsync(string json)
    {
        var schema = await Schema.Value.ConfigureAwait(false);
        var errors = new List<SchemaError>();
        Flatten(schema.Validate(json), errors);
        return errors
            .Distinct()
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ThenBy(e => e.Kind, StringComparer.Ordinal)
            .ToList();
    }

    private static void Flatten(IEnumerable<ValidationError> source, List<SchemaError> target)
    {
        foreach (var error in source)
        {
            // Array items and properties wrap the real cause: report the innermost error with its path.
            // oneOf/anyOf alternatives stay at the parent, their child errors would only list every branch.
            if (error is ChildSchemaValidationError child
                && error.Kind is ValidationErrorKind.ArrayItemNotValid or ValidationErrorKind.AdditionalItemNotValid or ValidationErrorKind.AdditionalPropertiesNotValid
                && child.Errors.Values.Any(e => e.Count > 0))
            {
                foreach (var nested in child.Errors.Values)
                {
                    Flatten(nested, target);
                }
                continue;
            }
            target.Add(new SchemaError(Normalize(error.Path), error.Kind.ToString()));
        }
    }

    private static string Normalize(string? path) => string.IsNullOrEmpty(path) ? "#" : path;

    private static Task<JsonSchema> LoadAsync(string file) =>
        JsonSchema.FromJsonAsync(ReadResource(file), VirtualRoot + file, schema => new EmbeddedResolver(schema));

    internal static string ReadResource(string file)
    {
        using var stream = typeof(SchemaValidator).Assembly.GetManifestResourceStream(ResourcePrefix + file)
            ?? throw new InvalidOperationException($"Eingebettetes Schema {file} fehlt.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class EmbeddedResolver : JsonReferenceResolver
    {
        private static readonly string[] Known = [SchemaFile, "spdx.schema.json", "jsf-0.82.schema.json"];

        public EmbeddedResolver(JsonSchema root)
            : base(new JsonSchemaAppender(root, new DefaultTypeNameGenerator()))
        {
        }

        public override Task<IJsonReference> ResolveFileReferenceAsync(string filePath, CancellationToken cancellationToken = default) =>
            ResolveEmbedded(filePath);

        public override Task<IJsonReference> ResolveUrlReferenceAsync(string url, CancellationToken cancellationToken = default) =>
            ResolveEmbedded(url);

        private static async Task<IJsonReference> ResolveEmbedded(string location)
        {
            var file = location.Split('#')[0].Split('/', '\\').Last();
            if (!Known.Contains(file, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Schema-Verweis auf {location} ist nicht eingebettet.");
            }
            return await LoadAsync(file).ConfigureAwait(false);
        }
    }
}

/// <summary>One schema violation: JSON path and NJsonSchema error kind (e.g. PropertyRequired).</summary>
public sealed record SchemaError(string Path, string Kind);
