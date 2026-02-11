# Data Model Analysis Report

## Executive Summary

Analysis of `data-model.md` revealed **2 critical implementation bugs** that have been fixed:

1. ? **Policy ID was NOT auto-generated** (now fixed ?)
2. ? **Person ID was auto-generated instead of using caller-provided value** (now fixed ?)

---

## Detailed Findings

### 1. Policy ID Generation (CRITICAL BUG - FIXED ?)

**Specification Requirement:**
- Policy IDs must be **auto-generated** by the engine
- Format: `{dossier_id}-{sequence_number}`
- First policy: `{dossier_id}-1`
- Second policy: `{dossier_id}-2`
- Sequence number based on mutation order (not from mutation properties)

**Original Bug:**
```csharp
// ? WRONG - was reading from mutation_properties
var policyId = GetStringProperty(mutation.MutationProperties, "policy_id");
```

**Fix Applied:**
```csharp
// ? CORRECT - auto-generated based on current policy count
var policySequenceNumber = currentSituation.Dossier.Policies.Length + 1;
var policyId = $"{currentSituation.Dossier.DossierId}-{policySequenceNumber}";
```

**Impact:** 
- Ensures policy IDs are unique and predictable
- Aligns with the specification's data model
- Removes dependency on client-provided policy_id

---

### 2. Person ID (CRITICAL BUG - FIXED ?)

**Specification Requirement:**
- `person_id` is **provided by the caller** in `create_dossier` mutation
- Should be extracted from `mutation_properties`

**Original Bug:**
```csharp
// ? WRONG - was auto-generating
var person = new Person(
    Guid.NewGuid(),  // Auto-generated
    "PARTICIPANT",
    personName,
    birthDate
);
```

**Fix Applied:**
```csharp
// ? CORRECT - extracted from mutation_properties
var personId = GetGuidProperty(mutation.MutationProperties, "person_id");
var person = new Person(
    personId,  // Caller-provided
    "PARTICIPANT",
    personName,
    birthDate
);
```

**Impact:**
- Allows caller to specify consistent person IDs
- Aligns with specification requirements
- Enables proper tracking across systems

---

### 3. Validation Rules (ADDED ?)

**Added validation for `add_policy` mutation:**

#### Salary Validation
```csharp
if (salary < 0)
{
    // Add CRITICAL message: "INVALID_SALARY"
}
```

#### Part-Time Factor Validation
```csharp
if (partTimeFactor < 0 || partTimeFactor > 1)
{
    // Add CRITICAL message: "INVALID_PART_TIME_FACTOR"
}
```

**Per data-model.md requirements:**
- ? Salary must be >= 0
- ? Part-time factor must be between 0 and 1

---

## Data Model Compliance

### ? Correctly Implemented

| Aspect | Status | Details |
|--------|--------|---------|
| Entity Hierarchy | ? | Situation ? Dossier ? Persons/Policies |
| Dossier Status | ? | "ACTIVE" ? "RETIRED" transition |
| Date Format | ? | ISO 8601 (YYYY-MM-DD) |
| UUID Format | ? | Standard UUID v4 |
| Sequential Processing | ? | Mutations processed in order |
| Initial State | ? | `{ "dossier": null }` |

### Status Transitions

```
Initial: dossier = null
   ? create_dossier
   ? status: "ACTIVE", retirement_date: null
   ? add_policy (can repeat)
   ? policies array grows
   ? apply_indexation (optional)
   ? salary values updated
   ? calculate_retirement_benefit
   ? status: "RETIRED", retirement_date: set, attainable_pension: calculated
   ? project_future_benefits (bonus, optional)
   ? projections array populated
```

---

## Key Data Relationships

### As Per Specification

1. **Situation ? Dossier:** 1:1 or 1:0 (null before create_dossier)
2. **Dossier ? Persons:** 1:1 (exactly one PARTICIPANT)
3. **Dossier ? Policies:** 1:N (zero or more policies)

### Implementation Notes

- Each dossier has **exactly one person** with role "PARTICIPANT"
- Person is created during `create_dossier` mutation
- Policies are added via `add_policy` mutations
- Policy count determines the sequence number for policy_id generation

---

## Mutation-Specific Behaviors

### create_dossier
- **Inputs:** `dossier_id`, `person_id`, `person_name`, `birth_date`
- **Creates:** Dossier with one person, empty policies array
- **Sets:** status = "ACTIVE", retirement_date = null

### add_policy
- **Inputs:** `scheme_id`, `employment_start_date`, `salary`, `part_time_factor`
- **Generates:** `policy_id` = `{dossier_id}-{sequence}`
- **Validates:** salary >= 0, part_time_factor in [0, 1]
- **Adds:** New policy to dossier.policies array

### apply_indexation
- **Inputs:** `indexation_rate`
- **Applies to:** All policies (salary and attainable_pension if set)
- **Formula:** `new_value = old_value * (1 + indexation_rate)`

### calculate_retirement_benefit
- **Inputs:** `retirement_date`, `accrual_rate`
- **Formula:** `attainable_pension = salary × part_time_factor × accrual_rate × years_of_service`
- **Updates:** status ? "RETIRED", sets retirement_date, calculates attainable_pension

### project_future_benefits (Bonus)
- **Inputs:** `projection_dates[]`, `expected_growth_rate`
- **Base:** `attainable_pension` or `salary × part_time_factor × 0.02`
- **Formula:** `projected = base × (1 + growth_rate)^years`

---

## Breaking Changes in Fixed Version

### ?? API Contract Changes

1. **create_dossier mutation now requires `person_id`**
   ```json
   "mutation_properties": {
     "dossier_id": "...",
     "person_id": "...",  // ? NOW REQUIRED
     "person_name": "...",
     "birth_date": "..."
   }
   ```

2. **add_policy mutation NO LONGER accepts `policy_id`**
   ```json
   "mutation_properties": {
     // "policy_id": "..."  // ? REMOVED - auto-generated now
     "scheme_id": "...",
     "employment_start_date": "...",
     "salary": 50000,
     "part_time_factor": 1.0
   }
   ```

### Migration Guide

**Old Request (Broken):**
```json
{
  "mutation_properties": {
    "dossier_id": "abc-123",
    "person_name": "John",
    "birth_date": "1980-01-01"
  }
}
```

**New Request (Fixed):**
```json
{
  "mutation_properties": {
    "dossier_id": "abc-123",
    "person_id": "def-456",  // ? Add this
    "person_name": "John",
    "birth_date": "1980-01-01"
  }
}
```

---

## Testing Recommendations

### Test Cases to Add

1. **Policy ID Sequence:**
   - Add 3 policies ? verify IDs are `{dossier_id}-1`, `-2`, `-3`
   
2. **Person ID Persistence:**
   - Create dossier with person_id ? verify response contains same ID

3. **Validation:**
   - Negative salary ? expect CRITICAL error
   - Part-time factor = 1.5 ? expect CRITICAL error
   - Part-time factor = 0.5 ? should succeed

4. **Indexation on Multiple Policies:**
   - Add 2 policies, apply indexation ? verify both salaries updated

5. **Retirement Calculation:**
   - Verify status changes to "RETIRED"
   - Verify retirement_date is set
   - Verify attainable_pension formula is correct

---

## Compliance Summary

| Requirement | Implemented | Notes |
|-------------|-------------|-------|
| Policy ID auto-generation | ? | Fixed - was broken |
| Person ID from caller | ? | Fixed - was broken |
| Salary validation (>= 0) | ? | Added |
| Part-time validation (0-1) | ? | Added |
| Sequential mutation processing | ? | Working |
| Dossier status transitions | ? | Working |
| Date format (ISO 8601) | ? | Working |
| UUID format (v4) | ? | Working |
| Initial situation (null dossier) | ? | Working |
| Exactly one PARTICIPANT | ? | Working |

---

## Conclusion

The implementation is now **compliant with data-model.md** after fixing the two critical bugs:
1. Policy ID auto-generation
2. Person ID from caller

All data structures, validation rules, and mutation behaviors now match the specification.
