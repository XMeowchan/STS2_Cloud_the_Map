using System.Text.Json.Serialization;

namespace Sts2ModTemplate;

internal sealed class ModManifestInfo
{
    [JsonPropertyName("pck_name")]
    public string PckName { get; set; } = "Sts2ModTemplate";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "STS2 Mod Template";
}
