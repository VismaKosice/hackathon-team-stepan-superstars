using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient Client;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Client = factory.CreateClient();
    }

    protected async Task<CalculationResponse> PostCalculationRequestAsync(CalculationRequest request)
    {
        var response = await Client.PostAsJsonAsync("/calculation-requests", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<CalculationResponse>(JsonOptions);
        result.Should().NotBeNull();
        return result!;
    }

    protected static CalculationRequest CreateBasicRequest(string tenantId, params CalculationMutation[] mutations)
    {
        return new CalculationRequest(
            TenantId: tenantId,
            CalculationInstructions: new CalculationInstructions(mutations)
        );
    }

    protected static DossierCreationCalculationMutation CreateDossierMutation(
        Guid mutationId,
        DateOnly actualAt,
        Guid dossierId,
        Guid personId,
        string name,
        DateOnly birthDate)
    {
        return new DossierCreationCalculationMutation(
            MutationId: mutationId,
            MutationDefinitionName: "create_dossier",
            ActualAt: actualAt,
            MutationProperties: new Dictionary<string, object>
            {
                ["dossier_id"] = dossierId.ToString(),
                ["person_id"] = personId.ToString(),
                ["name"] = name,
                ["birth_date"] = birthDate.ToString("yyyy-MM-dd")
            }
        );
    }

    protected static DossierCalculationMutation CreateAddPolicyMutation(
        Guid mutationId,
        DateOnly actualAt,
        Guid dossierId,
        string schemeId,
        DateOnly employmentStartDate,
        decimal salary,
        decimal partTimeFactor)
    {
        return new DossierCalculationMutation(
            MutationId: mutationId,
            MutationDefinitionName: "add_policy",
            ActualAt: actualAt,
            MutationProperties: new Dictionary<string, object>
            {
                ["scheme_id"] = schemeId,
                ["employment_start_date"] = employmentStartDate.ToString("yyyy-MM-dd"),
                ["salary"] = salary,
                ["part_time_factor"] = partTimeFactor
            },
            DossierId: dossierId.ToString()
        );
    }

    protected static DossierCalculationMutation CreateApplyIndexationMutation(
        Guid mutationId,
        DateOnly actualAt,
        Guid dossierId,
        decimal percentage,
        string? schemeId = null,
        DateOnly? effectiveBefore = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["percentage"] = percentage
        };

        if (schemeId != null)
            properties["scheme_id"] = schemeId;
        
        if (effectiveBefore.HasValue)
            properties["effective_before"] = effectiveBefore.Value.ToString("yyyy-MM-dd");

        return new DossierCalculationMutation(
            MutationId: mutationId,
            MutationDefinitionName: "apply_indexation",
            ActualAt: actualAt,
            MutationProperties: properties,
            DossierId: dossierId.ToString()
        );
    }

    protected static DossierCalculationMutation CreateCalculateRetirementMutation(
        Guid mutationId,
        DateOnly actualAt,
        Guid dossierId,
        DateOnly retirementDate)
    {
        return new DossierCalculationMutation(
            MutationId: mutationId,
            MutationDefinitionName: "calculate_retirement_benefit",
            ActualAt: actualAt,
            MutationProperties: new Dictionary<string, object>
            {
                ["retirement_date"] = retirementDate.ToString("yyyy-MM-dd")
            },
            DossierId: dossierId.ToString()
        );
    }

    protected void AssertSuccessResponse(CalculationResponse response, string tenantId, int expectedMutationCount)
    {
        // Metadata assertions
        response.CalculationMetadata.Should().NotBeNull();
        response.CalculationMetadata.CalculationId.Should().NotBeEmpty();
        response.CalculationMetadata.TenantId.Should().Be(tenantId);
        response.CalculationMetadata.CalculationOutcome.Should().Be("SUCCESS");
        response.CalculationMetadata.CalculationDurationMs.Should().BeGreaterThanOrEqualTo(0);

        // Result assertions
        response.CalculationResult.Should().NotBeNull();
        response.CalculationResult.Mutations.Should().HaveCount(expectedMutationCount);
        response.CalculationResult.InitialSituation.Should().NotBeNull();
        response.CalculationResult.EndSituation.Should().NotBeNull();
    }
}
