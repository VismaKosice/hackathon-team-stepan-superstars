using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationMetadata(
    [property: JsonPropertyName("calculation_id")] Guid CalculationId,
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_started_at")] DateTime CalculationStartedAt,
    [property: JsonPropertyName("calculation_completed_at")] DateTime CalculationCompletedAt,
    [property: JsonPropertyName("calculation_duration_ms")] long CalculationDurationMs,
    [property: JsonPropertyName("calculation_outcome")] string CalculationOutcome
);
