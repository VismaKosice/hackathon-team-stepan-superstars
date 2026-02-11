# Pension Calculation Engine API - Implementation Guide

## Quick Start

This implementation provides a complete Pension Calculation Engine API based on the specification in `api-spec.yaml`.

### Running the API

#### Using Docker:
```bash
docker build -t hackaton-api .
docker run -p 8080:8080 hackaton-api
```

#### Using .NET CLI:
```bash
cd HackatonAPI
dotnet run
```

### Testing the API

Use the provided `sample-request.json` file to test:

```bash
curl -X POST http://localhost:5000/calculation-requests \
  -H "Content-Type: application/json" \
  -d @sample-request.json
```

Or with PowerShell:
```powershell
$body = Get-Content sample-request.json -Raw
Invoke-RestMethod -Uri "http://localhost:5000/calculation-requests" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

## Implementation Structure

```
HackatonAPI/
??? Models/
?   ??? CalculationRequest.cs      # Request model
?   ??? CalculationResponse.cs     # Response model
?   ??? CalculationMutation.cs     # Polymorphic mutation types
?   ??? SimplifiedSituation.cs     # Dossier state
?   ??? CalculationMessage.cs      # Error/warning messages
?   ??? ErrorResponse.cs           # Error responses
??? Services/
?   ??? CalculationEngine.cs       # Core calculation logic
??? Program.cs                     # API endpoint configuration
```

## Features Implemented

? POST /calculation-requests endpoint
? Request validation (tenant_id format, mutations array)
? Polymorphic mutation deserialization
? Sequential mutation processing
? Five mutation types:
  - create_dossier
  - add_policy
  - apply_indexation
  - calculate_retirement_benefit
  - project_future_benefits
? Error handling with CRITICAL/WARNING messages
? Calculation metadata (timing, outcome)
? Snake_case JSON naming
? AOT-compatible with source-generated JSON serialization

## Mutation Processing Logic

### 1. create_dossier
- Creates new dossier with participant
- Properties: `dossier_id`, `person_name`, `birth_date`
- Validation: Ensures no dossier exists

### 2. add_policy
- Adds pension policy to dossier
- Properties: `policy_id`, `scheme_id`, `employment_start_date`, `salary`, `part_time_factor`
- Validation: Requires existing dossier

### 3. apply_indexation
- Applies percentage increase to salary and pension
- Properties: `indexation_rate` (e.g., 0.02 for 2%)
- Applies to all policies in dossier

### 4. calculate_retirement_benefit
- Calculates pension based on accrual formula
- Properties: `retirement_date`, `accrual_rate`
- Formula: `salary × part_time_factor × accrual_rate × years_of_service`
- Sets dossier status to RETIRED

### 5. project_future_benefits
- Projects pension values into the future
- Properties: `projection_dates` (array), `expected_growth_rate`
- Applies compound growth from current pension/salary

## Response Format

```json
{
  "calculation_metadata": {
    "calculation_id": "uuid",
    "tenant_id": "sample_tenant",
    "calculation_started_at": "timestamp",
    "calculation_completed_at": "timestamp",
    "calculation_duration_ms": 123,
    "calculation_outcome": "SUCCESS"
  },
  "calculation_result": {
    "messages": [],
    "end_situation": { ... },
    "initial_situation": { ... },
    "mutations": [ ... ]
  }
}
```

## Error Handling

- **400 Bad Request**: Invalid tenant_id format or missing mutations
- **500 Internal Server Error**: Unexpected exceptions
- **CRITICAL messages**: Mutation processing errors (e.g., dossier not found)
- **WARNING messages**: (Reserved for future use)

## Next Steps for Performance Optimization

1. Add benchmarking for mutation processing
2. Implement caching for repeated calculations
3. Optimize JSON serialization
4. Add parallel processing where applicable
5. Profile and optimize hot paths
