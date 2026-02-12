using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

public class ErrorScenarioTests : IntegrationTestBase
{
    public ErrorScenarioTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateDossier_DossierAlreadyExists_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier1 = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var createDossier2 = CreateDossierMutation(
            mutationId: Guid.Parse("a2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2020, 1, 2),
            dossierId: Guid.Parse("d3333333-3333-3333-3333-333333333333"),
            personId: Guid.Parse("d4444444-4444-4444-4444-444444444444"),
            name: "John Doe",
            birthDate: new DateOnly(1965, 3, 20)
        );

        var request = CreateBasicRequest("tenant001", createDossier1, createDossier2);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        // Should contain CRITICAL error
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "DOSSIER_ALREADY_EXISTS" && m.Level == "CRITICAL");

        // Should only process first mutation
        response.CalculationResult.Mutations.Should().HaveCount(2); // Both mutations in array but second one failed
        
        // End situation should reflect state before failed mutation
        response.CalculationResult.EndSituation.Situation.Dossier.Should().NotBeNull();
        response.CalculationResult.EndSituation.Situation.Dossier!.Value.DossierId.Should().Be(dossierId);
    }

    [Fact]
    public async Task CreateDossier_InvalidBirthDate_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(2030, 6, 15) // Future date
        );

        var request = CreateBasicRequest("tenant001", createDossier);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "INVALID_BIRTH_DATE" && m.Level == "CRITICAL");

        // Dossier should not be created
        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }

    [Fact]
    public async Task CreateDossier_EmptyName_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "", // Empty name
            birthDate: new DateOnly(1960, 6, 15)
        );

        var request = CreateBasicRequest("tenant001", createDossier);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "INVALID_NAME" && m.Level == "CRITICAL");

        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }

    [Fact]
    public async Task AddPolicy_WithoutDossier_ShouldFailWithCriticalError()
    {
        // Arrange - Try to add policy without creating dossier first
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var request = CreateBasicRequest("tenant001", addPolicy);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "DOSSIER_NOT_FOUND" && m.Level == "CRITICAL");

        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }

    [Fact]
    public async Task AddPolicy_InvalidSalary_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: -50000m, // Negative salary
            partTimeFactor: 1.0m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "INVALID_SALARY" && m.Level == "CRITICAL");

        // Dossier should exist but no policies
        response.CalculationResult.EndSituation.Situation.Dossier.Should().NotBeNull();
        response.CalculationResult.EndSituation.Situation.Dossier!.Value.Policies.Should().BeEmpty();
    }

    [Fact]
    public async Task AddPolicy_InvalidPartTimeFactor_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: 50000m,
            partTimeFactor: 1.5m // > 1
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "INVALID_PART_TIME_FACTOR" && m.Level == "CRITICAL");

        response.CalculationResult.EndSituation.Situation.Dossier!.Value.Policies.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyIndexation_WithoutDossier_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.03m
        );

        var request = CreateBasicRequest("tenant001", applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "DOSSIER_NOT_FOUND" && m.Level == "CRITICAL");

        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }

    [Fact]
    public async Task ApplyIndexation_NoPolicies_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.03m
        );

        var request = CreateBasicRequest("tenant001", createDossier, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "NO_POLICIES" && m.Level == "CRITICAL");
    }

    [Fact]
    public async Task CalculateRetirement_WithoutDossier_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2025, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2025, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "DOSSIER_NOT_FOUND" && m.Level == "CRITICAL");

        response.CalculationResult.EndSituation.Situation.Dossier.Should().BeNull();
    }

    [Fact]
    public async Task CalculateRetirement_NoPolicies_ShouldFailWithCriticalError()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2025, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2025, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "NO_POLICIES" && m.Level == "CRITICAL");
    }

    [Fact]
    public async Task CalculateRetirement_NotEligible_ShouldFailWithCriticalError()
    {
        // Arrange - Person too young and not enough years of service
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1990, 6, 15) // Born in 1990, will be ~32 in 2023
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2018, 1, 1), // Only 5 years of service
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2023, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2023, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        response.CalculationMetadata.CalculationOutcome.Should().Be("FAILURE");
        
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "NOT_ELIGIBLE" && m.Level == "CRITICAL");

        // Dossier should remain ACTIVE
        response.CalculationResult.EndSituation.Situation.Dossier!.Value.Status.Should().Be("ACTIVE");
        response.CalculationResult.EndSituation.Situation.Dossier!.Value.RetirementDate.Should().BeNull();
    }

    [Fact]
    public async Task CalculateRetirement_EligibleWith40YearsService_ShouldSucceed()
    {
        // Arrange - Person under 65 but with 40 years of service
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1980, 6, 15) // Born in 1980
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(1980, 1, 1), // 40 years of service
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2020, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);
        
        // Should succeed - 40 years of service even though under 65
        response.CalculationResult.EndSituation.Situation.Dossier!.Value.Status.Should().Be("RETIRED");
        response.CalculationResult.Messages.Should().BeEmpty();
    }
}
