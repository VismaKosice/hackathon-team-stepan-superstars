using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

public class BasicMutationTests : IntegrationTestBase
{
    public BasicMutationTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateDossier_Only_ShouldSucceed()
    {
        // Arrange
        var dossierId = Guid.Parse("d2222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("d3333333-3333-3333-3333-333333333333");
        var mutationId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

        var createDossier = CreateDossierMutation(
            mutationId: mutationId,
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            personId: personId,
            name: "Jane Doe",
            birthDate: new DateOnly(1960, 6, 15)
        );

        var request = CreateBasicRequest("tenant001", createDossier);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 1);

        // Verify initial situation
        response.CalculationResult.InitialSituation.Situation.Dossier.Should().BeNull();
        response.CalculationResult.InitialSituation.ActualAt.Should().Be(new DateOnly(2020, 1, 1));

        // Verify end situation
        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.DossierId.Should().Be(dossierId);
        dossier.Status.Should().Be("ACTIVE");
        dossier.RetirementDate.Should().BeNull();
        dossier.Persons.Should().HaveCount(1);
        dossier.Persons[0].PersonId.Should().Be(personId);
        dossier.Persons[0].Name.Should().Be("Jane Doe");
        dossier.Persons[0].BirthDate.Should().Be(new DateOnly(1960, 6, 15));
        dossier.Persons[0].Role.Should().Be("PARTICIPANT");
        dossier.Policies.Should().BeEmpty();

        // Verify no validation messages
        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDossier_AndAddSinglePolicy_ShouldSucceed()
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

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 2);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(1);

        var policy = dossier.Policies[0];
        policy.PolicyId.Should().Be($"{dossierId}-1");
        policy.SchemeId.Should().Be("SCHEME-A");
        policy.EmploymentStartDate.Should().Be(new DateOnly(2000, 1, 1));
        policy.Salary.Should().Be(50000m);
        policy.PartTimeFactor.Should().Be(1.0m);
        policy.AttainablePension.Should().BeNull();
        policy.Projections.Should().BeNull();

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDossier_AndAddMultiplePolicies_ShouldSucceed()
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

        var addPolicy3 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b3333333-3333-3333-3333-333333333333"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-C",
            employmentStartDate: new DateOnly(2015, 6, 1),
            salary: 70000m,
            partTimeFactor: 0.5m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2, addPolicy3);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 4);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier.Should().NotBeNull();
        dossier!.Policies.Should().HaveCount(3);

        // Verify policy IDs are sequential
        dossier.Policies[0].PolicyId.Should().Be($"{dossierId}-1");
        dossier.Policies[1].PolicyId.Should().Be($"{dossierId}-2");
        dossier.Policies[2].PolicyId.Should().Be($"{dossierId}-3");

        // Verify policy details
        dossier.Policies[0].SchemeId.Should().Be("SCHEME-A");
        dossier.Policies[0].Salary.Should().Be(50000m);
        dossier.Policies[0].PartTimeFactor.Should().Be(1.0m);

        dossier.Policies[1].SchemeId.Should().Be("SCHEME-B");
        dossier.Policies[1].Salary.Should().Be(60000m);
        dossier.Policies[1].PartTimeFactor.Should().Be(0.8m);

        dossier.Policies[2].SchemeId.Should().Be("SCHEME-C");
        dossier.Policies[2].Salary.Should().Be(70000m);
        dossier.Policies[2].PartTimeFactor.Should().Be(0.5m);

        response.CalculationResult.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AddPolicy_DuplicatePolicy_ShouldReturnWarning()
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

        // Same scheme_id and employment_start_date - should trigger WARNING
        var addPolicy2 = CreateAddPolicyMutation(
            mutationId: Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            actualAt: new DateOnly(2020, 1, 1),
            dossierId: dossierId,
            schemeId: "SCHEME-A",
            employmentStartDate: new DateOnly(2000, 1, 1),
            salary: 55000m,
            partTimeFactor: 1.0m
        );

        var request = CreateBasicRequest("tenant001", createDossier, addPolicy1, addPolicy2);

        // Act
        var response = await PostCalculationRequestAsync(request);

        // Assert
        AssertSuccessResponse(response, "tenant001", 3);

        var dossier = response.CalculationResult.EndSituation.Situation.Dossier;
        dossier!.Policies.Should().HaveCount(2); // Both policies are added despite warning

        // Verify warning message
        response.CalculationResult.Messages.Should().ContainSingle(m => 
            m.Code == "DUPLICATE_POLICY" && m.Level == "WARNING");
    }
}
