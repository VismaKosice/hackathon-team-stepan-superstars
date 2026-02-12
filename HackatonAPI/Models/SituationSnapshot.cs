using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct SituationSnapshot(
    [property: JsonPropertyName("actual_at")] DateOnly ActualAt,
    [property: JsonPropertyName("situation")] SimplifiedSituation Situation,
    [property: JsonPropertyName("mutation_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MutationId = null,
    [property: JsonPropertyName("mutation_index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MutationIndex = null
);
