# Root Cause Analysis: Response Differences

## ?? Issues Found & Fixed

### 1. ? CRITICAL FIX: JsonElement Serialization

**Problem:** The `mutation_properties` dictionary contains `JsonElement` values after deserialization. When re-serializing the response, these `JsonElement` objects were not being written correctly.

**Root Cause:**
```csharp
// IN: CalculationMutationConverter.Write()
writer.WritePropertyName("mutation_properties");
JsonSerializer.Serialize(writer, value.MutationProperties, options);
// ? This tries to serialize Dictionary<string, object> where values are JsonElement
```

**Fix Applied:**
```csharp
writer.WritePropertyName("mutation_properties");
writer.WriteStartObject();
foreach (var kvp in value.MutationProperties)
{
    writer.WritePropertyName(kvp.Key);
    if (kvp.Value is JsonElement element)
    {
        element.WriteTo(writer);  // ? Properly write JsonElement
    }
    else
    {
        JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
    }
}
writer.WriteEndObject();
```

**Impact:** mutation_properties now serialize correctly with original values instead of JsonElement wrappers.

---

### 2. ? FIX: calculation_message_indexes Should Be Null When Empty

**Problem:** Empty message indexes array was serialized as `[]` instead of `null` or being omitted.

**Expected (from README):**
```json
{
  "mutation": { ... }
  // calculation_message_indexes is not present when there are no messages
}
```

**Fix Applied:**
```csharp
// OLD
mutationResults.Add(new MutationResult(
    mutation,
    messageIndexes.ToArray()  // ? Always an array, even if empty
));

// NEW
mutationResults.Add(new MutationResult(
    mutation,
    messageIndexes.Count > 0 ? messageIndexes.ToArray() : null  // ? null when empty
));
```

**Impact:** Fields with null values are typically omitted in JSON serialization, matching the expected response format.

---

### 3. ? ADDED: Missing Primitive Type Registrations

**Problem:** Source-generated JSON might not handle all primitive types in nested structures.

**Added to source generation context:**
```csharp
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(DateTime))]
```

**Impact:** Ensures all value types serialize correctly in complex nested structures.

---

## ?? Verification Steps

To verify the fixes work correctly:

### 1. Start the API
```bash
cd HackatonAPI
dotnet run
```

### 2. Send Test Request
```bash
curl -X POST http://localhost:5000/calculation-requests \
  -H "Content-Type: application/json" \
  -d @test-readme-example.json
```

### 3. Compare With Expected Response

**Key fields to check:**

#### mutation_properties Should Contain Original Values
```json
"mutation_properties": {
  "dossier_id": "d2222222-2222-2222-2222-222222222222",  // ? String GUID
  "person_id": "p3333333-3333-3333-3333-333333333333",   // ? String GUID
  "name": "Jane Doe",                                     // ? String
  "birth_date": "1960-06-15"                              // ? Date string
}
// ? NOT like this: { "$type": "JsonElement", ... }
```

#### Salary After 3% Indexation
```json
"salary": 51500,  // or 51500.0 (both acceptable)
```

Calculation: `50000 * 1.03 = 51500` ?

#### Policy ID Format
```json
"policy_id": "d2222222-2222-2222-2222-222222222222-1"
```

Format: `{dossier_id}-{sequence}` ?

#### No calculation_message_indexes Field
```json
{
  "mutation": { ... }
  // calculation_message_indexes should be absent (null value omitted)
}
```

---

## ?? Other Potential Differences (Not Yet Fixed)

### A. DateTime Serialization Format

**Issue:** `calculation_started_at` and `calculation_completed_at` might not be in ISO 8601 format.

**Expected:**
```json
"calculation_started_at": "2024-01-15T10:30:00Z"
```

**Current:** Uses default DateTime serialization, which should be ISO 8601 but verify.

**To Fix (if needed):**
```csharp
// In CalculationMetadata, add custom converter or ensure ISO 8601 format
```

---

### B. Decimal Precision Display

**Issue:** Decimals might serialize with unnecessary precision.

**Examples:**
- `1.0` vs `1`
- `51500` vs `51500.0`
- `0.03` vs `0.030000`

**Note:** Both are valid JSON, but strict comparison might fail.

**Current:** Default decimal serialization. Should be acceptable.

---

### C. Property Omission for Null Values

**Issue:** Some JSON serializers include null values, others omit them.

**Example:**
```json
// Option 1: Include nulls
{
  "retirement_date": null,
  "attainable_pension": null,
  "projections": null
}

// Option 2: Omit nulls
{
  // Fields not present
}
```

**Current:** Default behavior includes nulls. Check if spec requires omission.

**To Fix (if needed):**
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // ...
});
```

---

## ?? Expected vs Actual Comparison

### Test Case: README.md Complete Example

**Input:** test-readme-example.json
```json
{
  "tenant_id": "tenant-001",
  "calculation_instructions": {
    "mutations": [
      { /* create_dossier */ },
      { /* add_policy */ },
      { /* apply_indexation with percentage: 0.03 */ }
    ]
  }
}
```

**Expected Output (Key Fields):**
```json
{
  "calculation_metadata": {
    "calculation_id": "<UUID>",
    "tenant_id": "tenant-001",
    "calculation_started_at": "<ISO 8601>",
    "calculation_completed_at": "<ISO 8601>",
    "calculation_duration_ms": <number>,
    "calculation_outcome": "SUCCESS"
  },
  "calculation_result": {
    "messages": [],
    "initial_situation": {
      "actual_at": "2020-01-01",
      "situation": {
        "dossier": null
      }
    },
    "mutations": [
      { "mutation": { /* create_dossier */ } },
      { "mutation": { /* add_policy */ } },
      { "mutation": { /* apply_indexation */ } }
    ],
    "end_situation": {
      "mutation_id": "c5555555-5555-5555-5555-555555555555",
      "mutation_index": 2,
      "actual_at": "2021-01-01",
      "situation": {
        "dossier": {
          "dossier_id": "d2222222-2222-2222-2222-222222222222",
          "status": "ACTIVE",
          "retirement_date": null,
          "persons": [
            {
              "person_id": "p3333333-3333-3333-3333-333333333333",
              "role": "PARTICIPANT",
              "name": "Jane Doe",
              "birth_date": "1960-06-15"
            }
          ],
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
        }
      }
    }
  }
}
```

---

## ?? Summary of Fixes

| Issue | Status | Impact |
|-------|--------|--------|
| JsonElement in mutation_properties | ? Fixed | HIGH - Critical for correct serialization |
| Empty message indexes as null | ? Fixed | MEDIUM - Cleaner response format |
| Missing primitive types | ? Fixed | LOW - Defensive, prevents potential issues |
| DateTime format | ?? Check | LOW - Default should be correct |
| Decimal precision | ?? Check | LOW - Both formats valid |
| Null property omission | ?? Check | LOW - Depends on spec strictness |

---

## ? Ready for Testing

The most critical issues have been fixed:
1. ? mutation_properties now serialize correctly
2. ? Empty message indexes are now null
3. ? All types registered for source generation

**Next Step:** Run the API and compare actual output with expected output to identify any remaining differences.
