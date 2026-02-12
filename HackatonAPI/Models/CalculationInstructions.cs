using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct CalculationInstructions(
    [property: JsonPropertyName("mutations")] CalculationMutation[] Mutations
);
