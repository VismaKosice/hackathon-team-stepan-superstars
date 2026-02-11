using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record Policy(
    [property: JsonPropertyName("policy_id")] string PolicyId,
    [property: JsonPropertyName("scheme_id")] string SchemeId,
    [property: JsonPropertyName("employment_start_date")] DateOnly EmploymentStartDate,
    [property: JsonPropertyName("salary")] decimal Salary,
    [property: JsonPropertyName("part_time_factor")] decimal PartTimeFactor,
    [property: JsonPropertyName("attainable_pension")] decimal? AttainablePension = null,
    [property: JsonPropertyName("projections")] Projection[]? Projections = null
);
