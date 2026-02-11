using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using HackatonAPI.Models;
using HackatonAPI.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    //options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddSingleton<ICalculationEngine, CalculationEngine>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/calculation-requests", async Task<Results<Ok<CalculationResponse>, BadRequest<ErrorResponse>, ProblemHttpResult>> (
CalculationRequest request,
ICalculationEngine calculationEngine) =>
{
    try
    {
        // Validate tenant_id pattern
        if (string.IsNullOrWhiteSpace(request.TenantId) || 
            request.TenantId.Length > 25 ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.TenantId, @"^[a-z0-9]+(?:_[a-z0-9]+)*$"))
        {
            return TypedResults.BadRequest(new ErrorResponse(
                400,
                "Invalid tenant_id format. Must be lowercase alphanumeric with underscores, max 25 characters."
            ));
        }
        
        // Validate mutations array
        if (request.CalculationInstructions?.Mutations == null || 
            request.CalculationInstructions.Mutations.Length == 0)
        {
            return TypedResults.BadRequest(new ErrorResponse(
                400,
                "At least one mutation is required in calculation_instructions.mutations"
            ));
        }
        
        var response = await calculationEngine.ProcessCalculationAsync(request);
        return TypedResults.Ok(response);
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Internal Server Error"
        );
    }
})
.WithName("AddCalculationRequest")
.WithOpenApi();

app.Run();

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(CalculationRequest))]
[JsonSerializable(typeof(CalculationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(CalculationMutation))]
[JsonSerializable(typeof(DossierCreationCalculationMutation))]
[JsonSerializable(typeof(DossierCalculationMutation))]
[JsonSerializable(typeof(CalculationInstructions))]
[JsonSerializable(typeof(CalculationMetadata))]
[JsonSerializable(typeof(CalculationResult))]
[JsonSerializable(typeof(SituationSnapshot))]
[JsonSerializable(typeof(MutationResult))]
[JsonSerializable(typeof(SimplifiedSituation))]
[JsonSerializable(typeof(Dossier))]
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Policy))]
[JsonSerializable(typeof(Projection))]
[JsonSerializable(typeof(CalculationMessage))]
[JsonSerializable(typeof(CalculationMutation[]))]
[JsonSerializable(typeof(CalculationMessage[]))]
[JsonSerializable(typeof(MutationResult[]))]
[JsonSerializable(typeof(Person[]))]
[JsonSerializable(typeof(Policy[]))]
[JsonSerializable(typeof(Projection[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(DateTime))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}

// Make the Program class accessible to integration tests
public partial class Program { }
