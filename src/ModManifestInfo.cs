using System.Text.Json.Serialization;

namespace Sts2ModTemplate;

internal sealed class ModManifestInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pck_name")]
    public string LegacyPckName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "STS2 Mod Template";

    public string ResolvedId =>
        !string.IsNullOrWhiteSpace(Id)
            ? Id
            : LegacyPckName;
}
