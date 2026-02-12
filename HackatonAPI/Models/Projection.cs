using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record Projection(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("projected_pension")] decimal ProjectedPension
);
