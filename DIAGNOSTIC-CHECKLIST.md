# Diagnostic Checklist for Response Comparison

## Recent Fixes Applied

### 1. ? CRITICAL Message Handling
**Fix:** Added check to halt processing when CRITICAL messages are detected
```csharp
if (messageIndexes.Any() && messages.Any(m => messageIndexes.Contains(m.Id) && m.Level == "CRITICAL"))
{
    break;
}
```

### 2. ? Null Field Omission
**Fix:** Added JsonIgnore(Condition = WhenWritingNull) to optional fields
- `mutation_id` in SituationSnapshot (for initial_situation)
- `mutation_index` in SituationSnapshot (for initial_situation)
- `calculation_message_indexes` in MutationResult (when no messages)
- Bonus patch fields

---

## Step-by-Step Verification

### Test 1: Basic Request Processing

**Input:** test-readme-example.json
```json
{
  "tenant_id": "tenant001",
  "calculation_instructions": {
    "mutations": [
      { /* create_dossier */ },
      { /* add_policy */ },
      { /* apply_indexation */ }
    ]
  }
}
```

**Expected Flow:**
1. Create dossier with ID d2222222-2222-2222-2222-222222222222
2. Add policy (auto-generated ID: d2222222-2222-2222-2222-222222222222-1)
3. Apply 3% indexation (50000 ? 51500)

---

### Test 2: Metadata Verification

Check these fields in `calculation_metadata`:

```json
{
  "calculation_id": "<UUID>",          // ? Any valid UUID
  "tenant_id": "tenant001",            // ? Must match exactly (no dashes!)
  "calculation_started_at": "<ISO>",   // ? ISO 8601 format
  "calculation_completed_at": "<ISO>", // ? ISO 8601 format
  "calculation_duration_ms": <number>, // ? Positive integer
  "calculation_outcome": "SUCCESS"     // ? Must be "SUCCESS" (not "FAILURE")
}
```

**Common Issues:**
- ? tenant_id has dashes: "tenant-001"
- ? calculation_outcome is "FAILURE" when it should be "SUCCESS"

---

### Test 3: Messages Array

```json
"messages": []
```

**Expected:**
- ? Empty array (length = 0)
- ? Not null
- ? No elements

**Common Issues:**
- ? Messages array contains validation errors
- ? Messages is null instead of []

---

### Test 4: Initial Situation

```json
"initial_situation": {
  "actual_at": "2020-01-01",
  "situation": {
    "dossier": null
  }
}
```

**Expected:**
- ? actual_at = first mutation's actual_at
- ? dossier = null (not an object)
- ? NO mutation_id field
- ? NO mutation_index field

**Common Issues:**
- ? dossier is {} instead of null
- ? mutation_id/mutation_index fields are present
- ? actual_at is wrong date

---

### Test 5: Mutations Array

Each mutation should look like:
```json
{
  "mutation": {
    "mutation_id": "...",
    "mutation_definition_name": "...",
    "mutation_type": "...",
    "actual_at": "...",
    "mutation_properties": { ... }
  }
  // NO calculation_message_indexes field when no messages
}
```

**Expected:**
- ? 3 mutations total
- ? Each has a "mutation" object
- ? mutation_properties contain ORIGINAL values (not JsonElement)
- ? NO calculation_message_indexes field (should be omitted, not null)

**Common Issues:**
- ? calculation_message_indexes: null (should be omitted entirely)
- ? calculation_message_indexes: [] (should be omitted entirely)
- ? mutation_properties has wrong structure

---

### Test 6: Mutation 1 Details (create_dossier)

```json
{
  "mutation": {
    "mutation_id": "a1111111-1111-1111-1111-111111111111",
    "mutation_definition_name": "create_dossier",
    "mutation_type": "DOSSIER_CREATION",
    "actual_at": "2020-01-01",
    "mutation_properties": {
      "dossier_id": "d2222222-2222-2222-2222-222222222222",
      "person_id": "p3333333-3333-3333-3333-333333333333",
      "name": "Jane Doe",
      "birth_date": "1960-06-15"
    }
  }
}
```

**Verify:**
- ? NO dossier_id at mutation level (only in mutation_properties)
- ? mutation_type = "DOSSIER_CREATION"
- ? mutation_properties.name (not person_name)

---

### Test 7: Mutation 2 Details (add_policy)

```json
{
  "mutation": {
    "mutation_id": "b4444444-4444-4444-4444-444444444444",
    "mutation_definition_name": "add_policy",
    "mutation_type": "DOSSIER",
    "actual_at": "2020-01-01",
    "dossier_id": "d2222222-2222-2222-2222-222222222222",
    "mutation_properties": {
      "scheme_id": "SCHEME-A",
      "employment_start_date": "2000-01-01",
      "salary": 50000,
      "part_time_factor": 1.0
    }
  }
}
```

**Verify:**
- ? mutation_type = "DOSSIER" (not "DOSSIER_CREATION")
- ? dossier_id at mutation level (for DOSSIER type)
- ? salary = 50000 (not 51500 yet - that's after indexation)

---

### Test 8: Mutation 3 Details (apply_indexation)

```json
{
  "mutation": {
    "mutation_id": "c5555555-5555-5555-5555-555555555555",
    "mutation_definition_name": "apply_indexation",
    "mutation_type": "DOSSIER",
    "actual_at": "2021-01-01",
    "dossier_id": "d2222222-2222-2222-2222-222222222222",
    "mutation_properties": {
      "percentage": 0.03
    }
  }
}
```

**Verify:**
- ? mutation_properties.percentage (not indexation_rate)
- ? percentage = 0.03 (not 3 or 3.0)

---

### Test 9: End Situation

```json
"end_situation": {
  "mutation_id": "c5555555-5555-5555-5555-555555555555",
  "mutation_index": 2,
  "actual_at": "2021-01-01",
  "situation": { ... }
}
```

**Verify:**
- ? mutation_id = last mutation's ID
- ? mutation_index = 2 (0-based: mutations are [0, 1, 2])
- ? actual_at = last mutation's actual_at

---

### Test 10: End Situation - Dossier

```json
"dossier": {
  "dossier_id": "d2222222-2222-2222-2222-222222222222",
  "status": "ACTIVE",
  "retirement_date": null,
  "persons": [ ... ],
  "policies": [ ... ]
}
```

**Verify:**
- ? status = "ACTIVE" (not "RETIRED")
- ? retirement_date = null (included but null)

---

### Test 11: End Situation - Person

```json
"persons": [
  {
    "person_id": "p3333333-3333-3333-3333-333333333333",
    "role": "PARTICIPANT",
    "name": "Jane Doe",
    "birth_date": "1960-06-15"
  }
]
```

**Verify:**
- ? Exactly 1 person
- ? All fields match create_dossier input

---

### Test 12: End Situation - Policy (CRITICAL)

```json
"policies": [
  {
    "policy_id": "d2222222-2222-2222-2222-222222222222-1",
    "scheme_id": "SCHEME-A",
    "employment_start_date": "2000-01-01",
    "salary": 51500,
    "part_time_factor": 1.0,
    "attainable_pension": null,
    "projections": null
  }
]
```

**VERIFY CAREFULLY:**
- ? policy_id = "d2222222-2222-2222-2222-222222222222-1" (with -1 suffix)
- ? salary = 51500 (AFTER indexation: 50000 * 1.03)
- ? attainable_pension = null (included but null)
- ? projections = null (included but null)

**Common Issues:**
- ? salary = 50000 (indexation not applied)
- ? salary = 51500.0 (extra decimal precision)
- ? policy_id wrong format

---

## Quick Verification Script

### PowerShell
```powershell
# Start API
cd HackatonAPI
Start-Process dotnet -ArgumentList "run" -NoNewWindow

# Wait for startup
Start-Sleep -Seconds 2

# Send request
$response = Invoke-RestMethod -Uri "http://localhost:5000/calculation-requests" `
  -Method Post `
  -ContentType "application/json" `
  -Body (Get-Content ..\test-readme-example.json -Raw)

# Check key fields
Write-Host "tenant_id: $($response.calculation_metadata.tenant_id)" # Should be tenant001
Write-Host "outcome: $($response.calculation_metadata.calculation_outcome)" # Should be SUCCESS
Write-Host "messages count: $($response.calculation_result.messages.Length)" # Should be 0
Write-Host "mutations count: $($response.calculation_result.mutations.Length)" # Should be 3
Write-Host "salary: $($response.calculation_result.end_situation.situation.dossier.policies[0].salary)" # Should be 51500
Write-Host "policy_id: $($response.calculation_result.end_situation.situation.dossier.policies[0].policy_id)" # Should end with -1
```

### Bash
```bash
# Start API
cd HackatonAPI
dotnet run &
API_PID=$!

# Wait for startup
sleep 2

# Send request
curl -X POST http://localhost:5000/calculation-requests \
  -H "Content-Type: application/json" \
  -d @../test-readme-example.json \
  | jq '{
      tenant_id: .calculation_metadata.tenant_id,
      outcome: .calculation_metadata.calculation_outcome,
      messages_count: (.calculation_result.messages | length),
      mutations_count: (.calculation_result.mutations | length),
      salary: .calculation_result.end_situation.situation.dossier.policies[0].salary,
      policy_id: .calculation_result.end_situation.situation.dossier.policies[0].policy_id
    }'

# Stop API
kill $API_PID
```

---

## Most Likely Issues

Based on common errors:

1. **tenant_id format**
   - ? "tenant-001" (with dashes)
   - ? "tenant001" (no dashes)

2. **calculation_outcome**
   - ? "FAILURE" (means CRITICAL messages exist)
   - ? "SUCCESS" (no CRITICAL messages)

3. **Salary after indexation**
   - ? 50000 (indexation not applied)
   - ? 51500 (correctly applied)

4. **Null fields serialization**
   - ? Fields present when they should be omitted
   - ? Fields omitted when null

5. **mutation_properties structure**
   - ? JsonElement wrappers
   - ? Actual primitive values

---

## If Still Wrong

Provide the ACTUAL response you're getting and I can compare field-by-field with the expected response.

Key fields to check:
1. calculation_outcome value
2. Salary value in end_situation
3. Presence/absence of calculation_message_indexes
4. mutation_properties structure
