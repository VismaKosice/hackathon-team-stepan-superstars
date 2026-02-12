using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationResult(
    [property: JsonPropertyName("messages")] CalculationMessage[] Messages,
    [property: JsonPropertyName("end_situation")] SituationSnapshot EndSituation,
    [property: JsonPropertyName("initial_situation")] SituationSnapshot InitialSituation,
    [property: JsonPropertyName("mutations")] MutationResult[] Mutations
);
