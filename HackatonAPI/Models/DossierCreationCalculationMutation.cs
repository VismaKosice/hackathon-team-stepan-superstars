using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record DossierCreationCalculationMutation(
    Guid MutationId,
    string MutationDefinitionName,
    DateOnly ActualAt,
    Dictionary<string, object> MutationProperties
) : CalculationMutation(MutationId, MutationDefinitionName, "DOSSIER_CREATION", ActualAt, MutationProperties);
