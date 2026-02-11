# Integration Tests

This directory contains comprehensive integration tests for the Pension Calculation Engine API.

## Test Organization

The tests are organized into the following test classes:

### 1. IntegrationTestBase
Base class providing shared functionality for all integration tests:
- HTTP client setup with WebApplicationFactory
- Helper methods for creating mutation objects
- Common assertion methods
- JSON serialization configuration

### 2. BasicMutationTests
Tests for basic mutation operations:
- ✅ `create_dossier` only
- ✅ `create_dossier` + `add_policy` (single)
- ✅ `create_dossier` + `add_policy` (multiple policies)
- ✅ Duplicate policy detection (WARNING scenario)

### 3. IndexationTests
Tests for `apply_indexation` mutation:
- ✅ No filters (apply to all policies)
- ✅ With `scheme_id` filter
- ✅ With `effective_before` filter
- ✅ No matching policies (WARNING scenario)
- ✅ Negative salary clamping (WARNING scenario)

### 4. RetirementCalculationTests
Tests for `calculate_retirement_benefit` mutation:
- ✅ Full flow: create + policies + indexation + retirement
- ✅ Single policy retirement calculation
- ✅ Multiple policies with different part-time factors
- ✅ Retirement before employment (WARNING scenario)
- ✅ Complex scenario with multiple indexations

### 5. ErrorScenarioTests
Tests for error handling and validation:

**create_dossier errors:**
- ✅ DOSSIER_ALREADY_EXISTS (CRITICAL)
- ✅ INVALID_BIRTH_DATE (CRITICAL)
- ✅ INVALID_NAME (CRITICAL)

**add_policy errors:**
- ✅ DOSSIER_NOT_FOUND (CRITICAL)
- ✅ INVALID_SALARY (CRITICAL)
- ✅ INVALID_PART_TIME_FACTOR (CRITICAL)

**apply_indexation errors:**
- ✅ DOSSIER_NOT_FOUND (CRITICAL)
- ✅ NO_POLICIES (CRITICAL)

**calculate_retirement_benefit errors:**
- ✅ DOSSIER_NOT_FOUND (CRITICAL)
- ✅ NO_POLICIES (CRITICAL)
- ✅ NOT_ELIGIBLE (CRITICAL - under 65 AND < 40 years service)
- ✅ Eligible with 40 years service (SUCCESS - even if under 65)

## Running the Tests

### Run all tests
```bash
dotnet test HackatonAPI.IntegrationTests
```

### Run specific test class
```bash
dotnet test HackatonAPI.IntegrationTests --filter "FullyQualifiedName~BasicMutationTests"
dotnet test HackatonAPI.IntegrationTests --filter "FullyQualifiedName~IndexationTests"
dotnet test HackatonAPI.IntegrationTests --filter "FullyQualifiedName~RetirementCalculationTests"
dotnet test HackatonAPI.IntegrationTests --filter "FullyQualifiedName~ErrorScenarioTests"
```

### Run specific test
```bash
dotnet test HackatonAPI.IntegrationTests --filter "FullyQualifiedName~CreateDossier_Only_ShouldSucceed"
```

### Run with detailed output
```bash
dotnet test HackatonAPI.IntegrationTests --verbosity detailed
```

## Test Coverage

The test suite covers all scenarios mentioned in the README.md:

### Correctness Scenarios (40 points)
- ✅ `create_dossier` only (4 points)
- ✅ `create_dossier` + `add_policy` (single) (4 points)
- ✅ `create_dossier` + `add_policy` (multiple policies) (4 points)
- ✅ `apply_indexation` (no filters) (4 points)
- ✅ `apply_indexation` with `scheme_id` filter (3 points)
- ✅ `apply_indexation` with `effective_before` filter (3 points)
- ✅ Full flow: create + policies + indexation + retirement (6 points)
- ✅ Multiple policies with different part-time factors + retirement (6 points)
- ✅ Error: retirement without eligibility (3 points)
- ✅ Error: mutation without dossier (3 points)

### Additional Test Coverage
- All CRITICAL error codes
- All WARNING scenarios
- Edge cases (negative indexation, retirement before employment, etc.)
- Complex multi-step scenarios

## Test Data Patterns

Tests use consistent GUID patterns for easy identification:
- Dossier IDs: `d2222222-2222-2222-2222-222222222222`
- Person IDs: `d3333333-3333-3333-3333-333333333333`
- Mutation IDs: Sequential prefixes (a1111111, b1111111, c1111111, etc.)

## Assertions

Tests use FluentAssertions for readable and maintainable assertions:
```csharp
response.CalculationMetadata.CalculationOutcome.Should().Be("SUCCESS");
dossier.Policies.Should().HaveCount(2);
response.CalculationResult.Messages.Should().BeEmpty();
```

## Dependencies

- xUnit - Test framework
- FluentAssertions - Assertion library
- Microsoft.AspNetCore.Mvc.Testing - Integration testing support
- Project reference to HackatonAPI

## Notes

- Tests use in-memory hosting via WebApplicationFactory
- Each test is independent and creates its own request
- Tests verify both happy paths and error scenarios
- Numeric comparisons use tolerance (0.01) for floating-point calculations
