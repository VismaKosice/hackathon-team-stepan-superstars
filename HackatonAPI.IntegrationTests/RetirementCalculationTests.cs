using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

public class RetirementCalculationTests : IntegrationTestBase
{
    public RetirementCalculationTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task FullFlow_CreateAddIndexRetire_ShouldCalculateCorrectly()
    {
        // Arrange - This is the example from README.md
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

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Value.Policies.Should().HaveCount(1);
        dossier.Value.Policies[0].Salary.Should().Be(51500m); // 50000 * 1.03
        dossier.Value.Status.Should().Be("ACTIVE");
        dossier.Value.RetirementDate.Should().BeNull();

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateRetirement_SinglePolicy_ShouldCalculateCorrectly()
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
            partTimeFactor: 1.0m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2025, 7, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2025, 7, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Value.Status.Should().Be("RETIRED");
        dossier.Value.RetirementDate.Should().Be(new DateOnly(2025, 7, 1));
        dossier.Value.Policies.Should().HaveCount(1);

        // Years of service: 2000-01-01 to 2025-07-01 = ~25.5 years
        // Weighted avg salary: 50000 (only one policy)
        // Annual pension = 50000 * 25.5 * 0.02 = 25,500
        var policy = dossier.Value.Policies[0];
        policy.AttainablePension.Should().NotBeNull();
        policy.AttainablePension.Should().BeApproximately(25500m, 100m); // Allow some tolerance for date calculation

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateRetirement_MultiplePoliciesWithDifferentPartTimeFactors_ShouldCalculateCorrectly()
    {
        // Arrange - Example from README
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
        dossier!.Value.Status.Should().Be("RETIRED");
        dossier!.Value.Policies.Should().HaveCount(2);

        // Years of service:
        // Policy 1: 25 years (2000-2025)
        // Policy 2: 15 years (2010-2025)
        // Total: 40 years

        // Effective salaries:
        // Policy 1: 50000 * 1.0 = 50000
        // Policy 2: 60000 * 0.8 = 48000

        // Weighted average: (50000 * 25 + 48000 * 15) / 40 = 49250
        // Annual pension: 49250 * 40 * 0.02 = 39,400

        // Distribution:
        // Policy 1: 39400 * (25/40) = 24,625
        // Policy 2: 39400 * (15/40) = 14,775

        dossier.Value.Policies[0].AttainablePension.Should().BeApproximately(24625m, 100m);
        dossier.Value.Policies[1].AttainablePension.Should().BeApproximately(14775m, 100m);

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateRetirement_RetirementBeforeEmployment_ShouldWarnButContinue()
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
            birthDate: new DateOnly(1925, 6, 15) // Born in 1925, will be 99 in 2025
        );

        var addPolicy = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2022, 1, 1), // Employment starts in 2022
            salary: 50000m,
            partTimeFactor: 1.0m
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2021, 1, 1) // Before employment start date (2022)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Value.Status.Should().Be("RETIRED");

        // Should have a warning about retirement before employment
        response.CalculationResult.Messages.Should().Contain(m => 
            m.Code == "RETIREMENT_BEFORE_EMPLOYMENT" && m.Level == "WARNING");

        // Policy should have 0 years of service, resulting in 0 pension
        dossier.Value.Policies[0].AttainablePension.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateRetirement_ComplexScenario_ShouldCalculateCorrectly()
    {
        // Arrange - Multi-policy with indexation
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
            salary: 40000m,
            partTimeFactor: 1.0m
        );

        var addPolicy2 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-B",
            employmentStartDate: new DateOnly(2010, 1, 1),
            salary: 50000m,
            partTimeFactor: 0.8m
        );

        var indexation1 = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c1111111-1111-1111-1111-111111111111"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.05m // 5% raise
        );

        var indexation2 = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2022, 1, 1),
            dossierId: dossierId,
            percentage: 0.03m // 3% raise
        );

        var calculateRetirement = CreateCalculateRetirementMutation(
            mutationId: Guid.Parse("d6666666-6666-6666-6666-666666666666"),
            actualAt: new DateOnly(2025, 1, 1),
            dossierId: dossierId,
            retirementDate: new DateOnly(2025, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", 
            createDossier, addPolicy1, addPolicy2, indexation1, indexation2, calculateRetirement);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 6);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Value.Status.Should().Be("RETIRED");
        dossier.Value.Policies.Should().HaveCount(2);

        // After indexations:
        // Policy 1: 40000 * 1.05 * 1.03 = 43260
        // Policy 2: 50000 * 1.05 * 1.03 = 54075

        // Verify salaries after indexation
        dossier.Value.Policies[0].Salary.Should().BeApproximately(43260m, 1m);
        dossier.Value.Policies[1].Salary.Should().BeApproximately(54075m, 1m);

        // Both policies should have calculated pensions
        dossier.Value.Policies[0].AttainablePension.Should().NotBeNull();
        dossier.Value.Policies[0].AttainablePension.Should().BeGreaterThan(0);
        dossier.Value.Policies[1].AttainablePension.Should().NotBeNull();
        dossier.Value.Policies[1].AttainablePension.Should().BeGreaterThan(0);

        response.CalculationResult.Messages.Should().BeEmpty();
    }
}
