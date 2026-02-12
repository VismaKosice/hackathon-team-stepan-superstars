using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record CalculationResponse(
    [property: JsonPropertyName("calculation_metadata")] CalculationMetadata CalculationMetadata,
    [property: JsonPropertyName("calculation_result")] CalculationResult CalculationResult
);
