using System.Text.Json.Serialization;
namespace SOLID.Middleware.Models;

public class SolidSubscription
{
    [JsonPropertyName("res-type")]
    public string ResType { get; set; } = "subscription";

    [JsonPropertyName("resource-name")]
    public string ResourceName { get; set; } = string.Empty;
    [JsonPropertyName("evt")]
    public int Evt { get; set; } // 1 = criação, 2 = eliminação 

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("dt-creation")]
    public string DtCreation { get; set; } = string.Empty;

    [JsonPropertyName("dt-expiration")]
    public string? DtExpiration { get; set; }
}