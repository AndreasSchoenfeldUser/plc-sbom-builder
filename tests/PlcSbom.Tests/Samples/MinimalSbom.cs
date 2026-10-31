using System.Text.Json.Nodes;

namespace PlcSbom.Tests.Samples;

/// <summary>
/// Smallest CycloneDX 1.6 document that passes schema and TR-03183-2 rules: a logical primary
/// component and one fully described application. Tests remove or change one thing at a time.
/// Synthetic test data, not from an adapter.
/// </summary>
public static class MinimalSbom
{
    public const string AppRef = "app";
    public const string PrimaryRef = "maschine";

    private static readonly string Sha512 = new('a', 128);

    private const string Json = """
        {
          "bomFormat": "CycloneDX",
          "specVersion": "1.6",
          "serialNumber": "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
          "version": 1,
          "metadata": {
            "timestamp": "2026-10-31T12:00:00Z",
            "manufacturer": { "name": "Musterbau GmbH", "contact": [ { "email": "sbom@musterbau.example" } ] },
            "component": {
              "type": "device", "bom-ref": "maschine", "name": "Maschine XY", "version": "R2026.10",
              "manufacturer": { "name": "Musterbau GmbH", "contact": [ { "email": "sbom@musterbau.example" } ] },
              "licenses": [ { "license": { "id": "MIT", "acknowledgement": "concluded" } } ],
              "properties": [ { "name": "plcsbom:logicalComponent", "value": "true" } ]
            }
          },
          "components": [
            {
              "type": "application", "bom-ref": "app", "name": "Anwenderprogramm", "version": "1.0.0",
              "manufacturer": { "name": "Musterbau GmbH", "url": [ "https://musterbau.example" ] },
              "licenses": [ { "license": { "id": "MIT", "acknowledgement": "concluded" } } ],
              "externalReferences": [
                { "type": "distribution", "url": "https://musterbau.example/app.bin",
                  "hashes": [ { "alg": "SHA-512", "content": "SHA512" } ] }
              ],
              "properties": [
                { "name": "bsi:component:filename", "value": "app.bin" },
                { "name": "bsi:component:executable", "value": "executable" },
                { "name": "bsi:component:archive", "value": "no archive" },
                { "name": "bsi:component:structured", "value": "unstructured" },
                { "name": "plcsbom:station", "value": "PLC_1" }
              ]
            }
          ],
          "dependencies": [
            { "ref": "maschine", "dependsOn": [ "app" ] },
            { "ref": "app", "dependsOn": [] }
          ],
          "compositions": [ { "aggregate": "complete", "dependencies": [ "maschine", "app" ] } ]
        }
        """;

    public static JsonObject Create() => JsonNode.Parse(Json.Replace("SHA512", Sha512, StringComparison.Ordinal))!.AsObject();

    public static JsonObject App(JsonObject bom) => bom["components"]![0]!.AsObject();

    public static JsonArray Properties(JsonObject component) => component["properties"]!.AsArray();

    public static void RemoveProperty(JsonObject component, string name)
    {
        var properties = Properties(component);
        foreach (var p in properties.OfType<JsonObject>().Where(p => p["name"]!.GetValue<string>() == name).ToList())
        {
            properties.Remove(p);
        }
    }

    public static void AddProperty(JsonObject component, string name, string value) =>
        Properties(component).Add(new JsonObject { ["name"] = name, ["value"] = value });
}
