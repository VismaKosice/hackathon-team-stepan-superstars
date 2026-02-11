using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record DossierCalculationMutation(
    Guid MutationId,
    string MutationDefinitionName,
    DateOnly ActualAt,
    Dictionary<string, object> MutationProperties,
    string DossierId
) : CalculationMutation(MutationId, MutationDefinitionName, "DOSSIER", ActualAt, MutationProperties);
