using System.Text.Json.Serialization;

namespace SOLID.Middleware.Models;

public class SolidApplication
{
    [JsonPropertyName("res-type")]
    public string ResType { get; set; } = string.Empty;

    [JsonPropertyName("resource-name")]
    public string? ResourceName { get; set; }

    [JsonPropertyName("resource-desc")]
    public string? ResourceDesc { get; set; }

    [JsonPropertyName("dt-creation")]
    public string? DtCreation { get; set; }
}
