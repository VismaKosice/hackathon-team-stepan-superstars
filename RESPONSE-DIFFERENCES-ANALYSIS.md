# Response Differences Analysis

## Potential Issues Identified

### 1. ? FIXED: JsonElement Serialization in mutation_properties

**Issue:** The custom `CalculationMutationConverter` was using `JsonSerializer.Serialize(writer, value.MutationProperties, options)` which would try to serialize a `Dictionary<string, object>` containing `JsonElement` values.

**Fix Applied:**
```csharp
// OLD - Would fail or produce wrong output
writer.WritePropertyName("mutation_properties");
JsonSerializer.Serialize(writer, value.MutationProperties, options);

// NEW - Properly handles JsonElement values
writer.WritePropertyName("mutation_properties");
writer.WriteStartObject();
foreach (var kvp in value.MutationProperties)
{
    writer.WritePropertyName(kvp.Key);
    if (kvp.Value is JsonElement element)
    {
        element.WriteTo(writer);
    }
    else
    {
        JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
    }
}
writer.WriteEndObject();
```

---

### 2. ? ADDED: Primitive Type Registrations

Added missing primitive types to source generation context:
- `string`
- `int`
- `decimal`
- `Guid`
- `DateOnly`
- `DateTime`

This ensures all value types in the response can be serialized.

---

## Known Differences to Check

### Check 1: Mutation Properties in Response

**Expected (from README):**
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

**What to verify:**
- mutation_properties contains the original values (not JsonElement wrapper)
- All GUIDs are properly formatted strings
- Dates are in YYYY-MM-DD format

---

### Check 2: Calculation Message Indexes

**Expected:**
```json
{
  "mutation": { ... },
  "calculation_message_indexes": []  // or null if no messages
}
```

**Current Implementation:**
```csharp
public record MutationResult(
    [property: JsonPropertyName("mutation")] CalculationMutation Mutation,
    [property: JsonPropertyName("calculation_message_indexes")] int[]? CalculationMessageIndexes = null,
    ...
);
```

**Potential Issue:** If `CalculationMessageIndexes` is an empty array vs null, this might differ from expected.

**Fix if needed:** Check if spec requires `null` or `[]` for no messages.

---

### Check 3: Nullable Fields in Response

Check these nullable fields match the spec:

**In Dossier:**
```json
{
  "retirement_date": null,  // Should be null for ACTIVE status
  ...
}
```

**In Policy:**
```json
{
  "attainable_pension": null,  // Should be null until retirement calculated
  "projections": null         // Should be null until projections calculated
}
```

---

### Check 4: Numeric Precision

**Salary after indexation:**
- Input: 50000
- Indexation: 0.03 (3%)
- Expected: 51500
- Formula: `50000 * (1 + 0.03) = 50000 * 1.03 = 51500`

**Current Implementation:**
```csharp
var newSalary = p.Salary * (1 + percentage);
```

This should be correct, but verify the decimal precision doesn't add extra digits.

---

### Check 5: End Situation Structure

**Expected:**
```json
"end_situation": {
  "mutation_id": "c5555555-5555-5555-5555-555555555555",
  "mutation_index": 2,
  "actual_at": "2021-01-01",
  "situation": {
    "dossier": { ... }
  }
}
```

**Current Implementation:**
```csharp
var endSituation = new SituationSnapshot(
    lastMutation.ActualAt,           // actual_at
    currentSituation,                 // situation
    lastMutation.MutationId,         // mutation_id
    mutationResults.Count - 1        // mutation_index (0-based)
);
```

**Verify:**
- mutation_index is 0-based (mutation 1 = index 0, mutation 3 = index 2) ?
- actual_at uses the last mutation's actual_at ?

---

### Check 6: Initial Situation Structure

**Expected:**
```json
"initial_situation": {
  "actual_at": "2020-01-01",  // First mutation's actual_at
  "situation": {
    "dossier": null           // Always null initially
  }
}
```

**Current Implementation:**
```csharp
var initialSituation = new SituationSnapshot(
    request.CalculationInstructions.Mutations[0].ActualAt,
    new SimplifiedSituation(null)
);
```

**Potential Issue:** `SituationSnapshot` constructor expects `mutation_id` and `mutation_index` but we're not providing them.

**Check the model:**
```csharp
public record SituationSnapshot(
    [property: JsonPropertyName("actual_at")] DateOnly ActualAt,
    [property: JsonPropertyName("situation")] SimplifiedSituation Situation,
    [property: JsonPropertyName("mutation_id")] Guid? MutationId = null,
    [property: JsonPropertyName("mutation_index")] int? MutationIndex = null
);
```

For `initial_situation`, `mutation_id` and `mutation_index` should be **omitted** (null), which is correct. ?

---

### Check 7: Messages Array

**Expected for success case:**
```json
"messages": []
```

**Current Implementation:**
```csharp
var messages = new List<CalculationMessage>();
// ...
new CalculationResult(
    messages.ToArray(),  // Will be empty array if no messages
    ...
)
```

This should be correct. ?

---

## Common Serialization Issues

### Issue A: Extra/Missing Properties

**Symptom:** Response has extra properties not in spec, or missing required properties.

**Check:**
1. All `[JsonPropertyName]` attributes match spec exactly
2. No properties are being serialized that shouldn't be
3. Required properties are not null when they should have values

---

### Issue B: Property Order

**Note:** JSON property order shouldn't matter for parsing, but some strict validators check order.

**Spec Order for mutation:**
1. mutation_id
2. mutation_definition_name
3. mutation_type
4. actual_at
5. dossier_id (if DOSSIER type)
6. mutation_properties

**Our Converter Order:** ? Matches spec

---

### Issue C: Date/Time Format

**Dates (DateOnly):**
- Format: `YYYY-MM-DD`
- Example: `"2020-01-01"`
- Current: `value.ActualAt.ToString("yyyy-MM-dd")` ?

**DateTimes (calculation_started_at, etc):**
- Format: ISO 8601
- Example: `"2024-01-15T10:30:00Z"`
- Current: Default DateTime serialization

**Potential Issue:** Check if DateTime is serializing in correct ISO 8601 format.

---

### Issue D: Decimal Formatting

**Expected:**
```json
{
  "salary": 51500,
  "part_time_factor": 1.0
}
```

**Potential Issue:** Decimals might serialize as:
- `51500.0` instead of `51500`
- `1.00` instead of `1.0`

**Note:** JSON standard allows both, but strict comparison might fail.

---

## Testing Checklist

Run these checks on actual vs expected response:

- [ ] All property names are snake_case
- [ ] mutation_properties contains original values (not wrapped)
- [ ] salary after 3% indexation = 51500 (exactly)
- [ ] policy_id = "d2222222-2222-2222-2222-222222222222-1"
- [ ] initial_situation.situation.dossier = null
- [ ] end_situation.mutation_index = 2 (for 3 mutations)
- [ ] messages = [] (empty array)
- [ ] retirement_date = null (ACTIVE status)
- [ ] attainable_pension = null (no retirement calculated)
- [ ] Dates in YYYY-MM-DD format
- [ ] No extra/missing properties

---

## Quick Test Command

If running locally:
```bash
curl -X POST http://localhost:5000/calculation-requests \
  -H "Content-Type: application/json" \
  -d @test-readme-example.json \
  | jq . > actual-response.json
```

Then compare with expected response from README.md.

---

## Most Likely Issues

Based on the fixes applied:

1. ? **FIXED:** JsonElement serialization in mutation_properties
2. ?? **CHECK:** DateTime format (ISO 8601)
3. ?? **CHECK:** Decimal precision (51500 vs 51500.0)
4. ?? **CHECK:** Empty array vs null for calculation_message_indexes

---

## Next Steps

1. **Run the API** and send test-readme-example.json
2. **Compare output** field by field with README expected response
3. **Identify specific differences** (property names, values, types)
4. **Apply targeted fixes** based on actual differences found
