using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationMessage(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message
);
