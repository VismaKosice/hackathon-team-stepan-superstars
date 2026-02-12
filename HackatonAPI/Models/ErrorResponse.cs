using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record ErrorResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message
);
