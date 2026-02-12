using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationResponse(
    [property: JsonPropertyName("calculation_metadata")] CalculationMetadata CalculationMetadata,
    [property: JsonPropertyName("calculation_result")] CalculationResult CalculationResult
);
