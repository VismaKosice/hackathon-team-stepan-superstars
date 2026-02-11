using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record CalculationInstructions(
    [property: JsonPropertyName("mutations")] CalculationMutation[] Mutations
);
