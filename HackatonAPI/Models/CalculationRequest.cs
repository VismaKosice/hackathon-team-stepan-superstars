using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationRequest(
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_instructions")] CalculationInstructions CalculationInstructions
);
