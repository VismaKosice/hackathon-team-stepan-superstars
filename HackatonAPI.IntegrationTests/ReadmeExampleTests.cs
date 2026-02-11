using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

/// <summary>
/// Tests that verify the exact examples from README.md
/// </summary>
public class ReadmeExampleTests : IntegrationTestBase
{
    public ReadmeExampleTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ReadmeExample_CreateAddIndexation_ShouldMatchExpectedResponse()
    {
        // This is the exact example from README.md "Complete Example" section
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
            partTimeFactor: 1.0m
        );

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.03m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert - Verify calculation metadata
        response.CalculationMetadata.Should().NotBeNull();
        response.CalculationMetadata.CalculationId.Should().NotBeEmpty();
        response.CalculationMetadata.TenantId.Should().Be("tenant001");
        response.CalculationMetadata.CalculationOutcome.Should().Be("SUCCESS");
        response.CalculationMetadata.CalculationDurationMs.Should().BeGreaterThanOrEqualTo(0);

        // Verify calculation result structure
        response.CalculationResult.Should().NotBeNull();
        response.CalculationResult.Messages.Should().BeEmpty();
        
        // Verify initial situation
        response.CalculationResult.InitialSituation.Should().NotBeNull();
        response.CalculationResult.InitialSituation.ActualAt.Should().Be(new DateOnly(2020, 1, 1));
        response.CalculationResult.InitialSituation.Situation.Dossier.Should().BeNull();

        // Verify mutations were processed
        response.CalculationResult.Mutations.Should().HaveCount(3);
        response.CalculationResult.Mutations[0].Mutation.MutationDefinitionName.Should().Be("create_dossier");
        response.CalculationResult.Mutations[1].Mutation.MutationDefinitionName.Should().Be("add_policy");
        response.CalculationResult.Mutations[2].Mutation.MutationDefinitionName.Should().Be("apply_indexation");

        // Verify end situation
        var endSituation = response.CalculationResult.EndSituation;
        endSituation.Should().NotBeNull();
        endSituation.MutationId.Should().Be(Guid.Parse("c5555555-5555-5555-5555-555555555555"));
        endSituation.MutationIndex.Should().Be(2); // 0-based index
        endSituation.ActualAt.Should().Be(new DateOnly(2021, 1, 1));

        // Verify dossier state
        var dossier = endSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.DossierId.Should().Be(dossierId);
        dossier.Status.Should().Be("ACTIVE");
        dossier.RetirementDate.Should().BeNull();

        // Verify person
        dossier.Persons.Should().HaveCount(1);
        var person = dossier.Persons[0];
        person.PersonId.Should().Be(personId);
        person.Role.Should().Be("PARTICIPANT");
        person.Name.Should().Be("Jane Doe");
        person.BirthDate.Should().Be(new DateOnly(1960, 6, 15));

        // Verify policy
        dossier.Policies.Should().HaveCount(1);
        var policy = dossier.Policies[0];
        policy.PolicyId.Should().Be($"{dossierId}-1");
        policy.SchemeId.Should().Be("SCHEME-A");
        policy.EmploymentStartDate.Should().Be(new DateOnly(2000, 1, 1));
        
        // Key verification: salary after 3% indexation
        // 50000 * 1.03 = 51500
        policy.Salary.Should().Be(51500m);
        
        policy.PartTimeFactor.Should().Be(1.0m);
        policy.AttainablePension.Should().BeNull();
        policy.Projections.Should().BeNull();
    }

    [Fact]
    public async Task ReadmeExample_RetirementCalculation_ShouldCalculateCorrectly()
    {
        // This verifies the retirement calculation example from README
        // Example: Policy 1 (25 years) + Policy 2 (15 years) = 40 years total
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

        var addPolicy1 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var addPolicy2 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-B",
            employmentStartDate: new DateOnly(2010, 1, 1),
            salary: 60000m,
            partTimeFactor: 0.8m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2025, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2025, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Status.Should().Be("RETIRED");
        dossier.RetirementDate.Should().Be(new DateOnly(2025, 1, 1));
        dossier.Policies.Should().HaveCount(2);

        // Verify the calculations from README example:
        // Years of service:
        // - Policy 1: 25 years (2000-2025)
        // - Policy 2: 15 years (2010-2025)
        // Total: 40 years
        //
        // Effective salaries:
        // - Policy 1: 50000 * 1.0 = 50000
        // - Policy 2: 60000 * 0.8 = 48000
        //
        // Weighted average: (50000 * 25 + 48000 * 15) / 40 = 49250
        // Annual pension: 49250 * 40 * 0.02 = 39,400
        //
        // Distribution:
        // - Policy 1: 39400 * (25/40) = 24,625
        // - Policy 2: 39400 * (15/40) = 14,775

        dossier.Policies[0].AttainablePension.Should().NotBeNull();
        dossier.Policies[1].AttainablePension.Should().NotBeNull();

        // Allow tolerance for date calculation precision
        dossier.Policies[0].AttainablePension.Should().BeApproximately(24625m, 100m);
        dossier.Policies[1].AttainablePension.Should().BeApproximately(14775m, 100m);

        // Verify total pension
        var totalPension = dossier.Policies[0].AttainablePension!.Value + 
                          dossier.Policies[1].AttainablePension!.Value;
        totalPension.Should().BeApproximately(39400m, 200m);

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadmeExample_PolicyIdGeneration_ShouldUseSequentialNumbers()
    {
        // Verify policy_id generation: {dossier_id}-{sequence_number}
        // Arrange
        var dossierId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var personId = Guid.Parse("660e8400-e29b-41d4-a716-446655440001");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Test Person",
            birthDate: new DateOnly(1970, 1, 1)
        );

        var addPolicy1 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-X",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: 40000m,
            partTimeFactor: 1.0m
        );

        var addPolicy2 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-Y",
            employmentStartDate: new DateOnly(2010, 1, 1),
            salary: 45000m,
            partTimeFactor: 0.9m
        );

        var addPolicy3 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b3333333-3333-3333-3333-333333333333"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-Z",
            employmentStartDate: new DateOnly(2015, 1, 1),
            salary: 50000m,
            partTimeFactor: 0.8m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, addPolicy3);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var policies = response.CalculationResult.EndSituation.Situation.Dossier!.Policies;
        policies.Should().HaveCount(3);

        // Verify sequential policy IDs
        policies[0].PolicyId.Should().Be("550e8400-e29b-41d4-a716-446655440000-1");
        policies[1].PolicyId.Should().Be("550e8400-e29b-41d4-a716-446655440000-2");
        policies[2].PolicyId.Should().Be("550e8400-e29b-41d4-a716-446655440000-3");
    }

    [Fact]
    public async Task ReadmeExample_EligibilityCheck_Under65With40Years_ShouldSucceed()
    {
        // Verify eligibility: Age >= 65 OR Years of Service >= 40
        // This tests the OR condition
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");

        var createDossier = CreateDossierMutation(
            mutationId: Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Early Retiree",
            birthDate: new DateOnly(1985, 1, 1) // Will be 45 in 2030
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(1990, 1, 1), // 40 years by 2030
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2030, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2030, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert - Should succeed despite being under 65 because of 40 years service
        AssertSuccessResponse(response, "tenant001", 3);
        
        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier!.Status.Should().Be("RETIRED");
        dossier.Policies[0].AttainablePension.Should().NotBeNull();
        dossier.Policies[0].AttainablePension.Should().BeGreaterThan(0);

        response.CalculationResult.Messages.Should().BeEmpty();
    }
}
