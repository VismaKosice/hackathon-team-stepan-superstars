using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record MutationResult(
    [property: JsonPropertyName("mutation")] CalculationMutation Mutation,
    [property: JsonPropertyName("calculation_message_indexes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int[]? CalculationMessageIndexes = null,
    [property: JsonPropertyName("forward_patch_to_situation_after_this_mutation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object[]? ForwardPatchToSituationAfterThisMutation = null,
    [property: JsonPropertyName("backward_patch_to_previous_situation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object[]? BackwardPatchToPreviousSituation = null
);
