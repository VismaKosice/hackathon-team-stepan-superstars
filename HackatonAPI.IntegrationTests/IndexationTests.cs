using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

public class IndexationTests : IntegrationTestBase
{
    public IndexationTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ApplyIndexation_NoFilters_ShouldUpdateAllPolicies()
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

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.03m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(2);

        // Verify both policies were indexed by 3%
        dossier.Policies[0].Salary.Should().Be(51500m); // 50000 * 1.03
        dossier.Policies[1].Salary.Should().Be(61800m); // 60000 * 1.03

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyIndexation_WithSchemeIdFilter_ShouldUpdateMatchingPoliciesOnly()
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

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.05m,
            schemeId: "SCHEME-A"
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(2);

        // Only SCHEME-A should be indexed by 5%
        dossier.Policies[0].Salary.Should().Be(52500m); // 50000 * 1.05
        dossier.Policies[1].Salary.Should().Be(60000m); // Unchanged

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyIndexation_WithEffectiveBeforeFilter_ShouldUpdateMatchingPoliciesOnly()
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
            employmentStartDate: new DateOnly(2015, 1, 1),
            salary: 60000m,
            partTimeFactor: 0.8m
        );

        var applyIndexation = CreateApplyIndexationMutation(
            mutationId: Guid.Parse("c5555555-5555-5555-5555-555555555555"),
            actualAt: new DateOnly(2021, 1, 1),
            dossierId: dossierId,
            percentage: 0.04m,
            effectiveBefore: new DateOnly(2010, 1, 1)
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(2);

        // Only policy with employment_start_date before 2010-01-01 should be indexed
        dossier.Policies[0].Salary.Should().Be(52000m); // 50000 * 1.04
        dossier.Policies[1].Salary.Should().Be(60000m); // Unchanged (2015 is not before 2010)

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyIndexation_NoMatchingPolicies_ShouldReturnWarning()
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
            mutationId: Guid.Parse("b1111111-1111-1111-1111-111111111111"),
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
            percentage: 0.03m,
            schemeId: "SCHEME-B" // No policy with this scheme_id
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(1);

        // Salary should remain unchanged
        dossier.Policies[0].Salary.Should().Be(50000m);

        // Should have a warning about no matching policies
        response.CalculationResult.Messages.Should().ContainSingle(m => 
            m.Code == "NO_MATCHING_POLICIES" && m.Level == "WARNING");
    }

    [Fact]
    public async Task ApplyIndexation_NegativeSalary_ShouldClampToZeroAndWarn()
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
            mutationId: Guid.Parse("b1111111-1111-1111-1111-111111111111"),
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
            percentage: -1.5m // -150% would make salary negative
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy, applyIndexation);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(1);

        // Salary should be clamped to 0
        dossier.Policies[0].Salary.Should().Be(0m);

        // Should have a warning about negative salary
        response.CalculationResult.Messages.Should().ContainSingle(m => 
            m.Code == "NEGATIVE_SALARY_CLAMPED" && m.Level == "WARNING");
    }
}
