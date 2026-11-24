using System.Text.Json.Serialization;
namespace SOLID.Middleware.Models;

public class SolidDataRecord
{
    [JsonPropertyName("res-type")]
    public string ResType { get; set; } = "data-record";
    
    [JsonPropertyName("resource-name")]
    public string ResourceName { get; set; } = string.Empty;
    
    [JsonPropertyName("content-type")]
    public string ContentType { get; set; } = "text/plain";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("dt-creation")]
    public string DtCreation { get; set; } = string.Empty;

    [JsonPropertyName("dt-expiration")]
    public string? DtExpiration { get; set; }
}