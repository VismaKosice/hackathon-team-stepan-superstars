using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public record SimplifiedSituation(
    [property: JsonPropertyName("dossier")] Dossier? Dossier
);
