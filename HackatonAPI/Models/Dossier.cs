using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct Dossier(
    [property: JsonPropertyName("dossier_id")] Guid DossierId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("retirement_date")] DateOnly? RetirementDate,
    [property: JsonPropertyName("persons")] Person[] Persons,
    [property: JsonPropertyName("policies")] Policy[] Policies
);
