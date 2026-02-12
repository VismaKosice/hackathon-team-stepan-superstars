using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct ErrorResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message
);
