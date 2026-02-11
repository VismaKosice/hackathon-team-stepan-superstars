using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record CalculationRequest(
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_instructions")] CalculationInstructions CalculationInstructions
);

public record CalculationInstructions(
    [property: JsonPropertyName("mutations")] CalculationMutation[] Mutations
);
