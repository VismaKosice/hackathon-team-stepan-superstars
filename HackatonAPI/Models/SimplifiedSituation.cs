using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record SimplifiedSituation(
    [property: JsonPropertyName("dossier")] Dossier? Dossier
);

public record Dossier(
    [property: JsonPropertyName("dossier_id")] Guid DossierId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("retirement_date")] DateOnly? RetirementDate,
    [property: JsonPropertyName("persons")] Person[] Persons,
    [property: JsonPropertyName("policies")] Policy[] Policies
);

public record Person(
    [property: JsonPropertyName("person_id")] Guid PersonId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("birth_date")] DateOnly BirthDate
);

public record Policy(
    [property: JsonPropertyName("policy_id")] string PolicyId,
    [property: JsonPropertyName("scheme_id")] string SchemeId,
    [property: JsonPropertyName("employment_start_date")] DateOnly EmploymentStartDate,
    [property: JsonPropertyName("salary")] decimal Salary,
    [property: JsonPropertyName("part_time_factor")] decimal PartTimeFactor,
    [property: JsonPropertyName("attainable_pension")] decimal? AttainablePension = null,
    [property: JsonPropertyName("projections")] Projection[]? Projections = null
);

public record Projection(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("projected_pension")] decimal ProjectedPension
);
