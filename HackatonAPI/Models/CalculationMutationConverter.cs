using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackatonAPI.Models;

public class CalculationMutationConverter : JsonConverter<CalculationMutation>
{
    public override CalculationMutation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("mutation_type", out var mutationTypeElement))
        {
            throw new JsonException("Missing mutation_type property");
        }

        var mutationType = mutationTypeElement.GetString();
        var mutationId = root.GetProperty("mutation_id").GetGuid();
        var mutationDefinitionName = root.GetProperty("mutation_definition_name").GetString()!;
        var actualAt = DateOnly.Parse(root.GetProperty("actual_at").GetString()!);
        
        var mutationProperties = new Dictionary<string, object>();
        if (root.TryGetProperty("mutation_properties", out var propsElement))
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                mutationProperties[prop.Name] = prop.Value.Clone();
            }
        }

        return mutationType switch
        {
            "DOSSIER_CREATION" => new DossierCreationCalculationMutation(
                mutationId,
                mutationDefinitionName,
                actualAt,
                mutationProperties
            ),
            "DOSSIER" => new DossierCalculationMutation(
                mutationId,
                mutationDefinitionName,
                actualAt,
                mutationProperties,
                root.GetProperty("dossier_id").GetString()!
            ),
            _ => throw new JsonException($"Unknown mutation_type: {mutationType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, CalculationMutation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WriteString("mutation_id", value.MutationId);
        writer.WriteString("mutation_definition_name", value.MutationDefinitionName);
        writer.WriteString("mutation_type", value.MutationType);
        writer.WriteString("actual_at", value.ActualAt.ToString("yyyy-MM-dd"));
        
        if (value is DossierCalculationMutation dossierMutation)
        {
            writer.WriteString("dossier_id", dossierMutation.DossierId);
        }
        
        // Serialize mutation_properties manually to handle JsonElement values
        writer.WritePropertyName("mutation_properties");
        writer.WriteStartObject();
        foreach (var kvp in value.MutationProperties)
        {
            writer.WritePropertyName(kvp.Key);
            
            if (kvp.Value is JsonElement element)
            {
                element.WriteTo(writer);
            }
            else
            {
                JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
            }
        }
        writer.WriteEndObject();
        
        writer.WriteEndObject();
    }
}
