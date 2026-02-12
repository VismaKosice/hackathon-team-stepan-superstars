using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct Projection(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("projected_pension")] decimal ProjectedPension
);
