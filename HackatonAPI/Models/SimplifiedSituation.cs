using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record struct SimplifiedSituation(
    [property: JsonPropertyName("dossier")] Dossier? Dossier
);
