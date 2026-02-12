using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record Person(
    [property: JsonPropertyName("person_id")] Guid PersonId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("birth_date")] DateOnly BirthDate
);
