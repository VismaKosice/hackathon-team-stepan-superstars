using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record CalculationResponse(
    [property: JsonPropertyName("calculation_metadata")] CalculationMetadata CalculationMetadata,
    [property: JsonPropertyName("calculation_result")] CalculationResult CalculationResult
);

public record CalculationMetadata(
    [property: JsonPropertyName("calculation_id")] Guid CalculationId,
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("calculation_started_at")] DateTime CalculationStartedAt,
    [property: JsonPropertyName("calculation_completed_at")] DateTime CalculationCompletedAt,
    [property: JsonPropertyName("calculation_duration_ms")] long CalculationDurationMs,
    [property: JsonPropertyName("calculation_outcome")] string CalculationOutcome
);

public record CalculationResult(
    [property: JsonPropertyName("messages")] CalculationMessage[] Messages,
    [property: JsonPropertyName("end_situation")] SituationSnapshot EndSituation,
    [property: JsonPropertyName("initial_situation")] SituationSnapshot InitialSituation,
    [property: JsonPropertyName("mutations")] MutationResult[] Mutations
);

public record SituationSnapshot(
    [property: JsonPropertyName("actual_at")] DateOnly ActualAt,
    [property: JsonPropertyName("situation")] SimplifiedSituation Situation,
    [property: JsonPropertyName("mutation_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MutationId = null,
    [property: JsonPropertyName("mutation_index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MutationIndex = null
);

public record MutationResult(
    [property: JsonPropertyName("mutation")] CalculationMutation Mutation,
    [property: JsonPropertyName("calculation_message_indexes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int[]? CalculationMessageIndexes = null,
    [property: JsonPropertyName("forward_patch_to_situation_after_this_mutation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object[]? ForwardPatchToSituationAfterThisMutation = null,
    [property: JsonPropertyName("backward_patch_to_previous_situation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object[]? BackwardPatchToPreviousSituation = null
);
