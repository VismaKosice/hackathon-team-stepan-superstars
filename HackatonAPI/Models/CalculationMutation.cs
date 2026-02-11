using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

[JsonConverter(typeof(CalculationMutationConverter))]
public abstract record CalculationMutation(
    Guid MutationId,
    string MutationDefinitionName,
    string MutationType,
    DateOnly ActualAt,
    Dictionary<string, object> MutationProperties
);

public record DossierCreationCalculationMutation(
    Guid MutationId,
    string MutationDefinitionName,
    DateOnly ActualAt,
    Dictionary<string, object> MutationProperties
) : CalculationMutation(MutationId, MutationDefinitionName, "DOSSIER_CREATION", ActualAt, MutationProperties);

public record DossierCalculationMutation(
    Guid MutationId,
    string MutationDefinitionName,
    DateOnly ActualAt,
    Dictionary<string, object> MutationProperties,
    string DossierId
) : CalculationMutation(MutationId, MutationDefinitionName, "DOSSIER", ActualAt, MutationProperties);
