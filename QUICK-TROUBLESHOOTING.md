# ?? Quick Troubleshooting Guide

## If Response is Still Wrong

### Step 1: Run the Test
```bash
cd HackatonAPI
dotnet run
```

In another terminal:
```bash
curl -X POST http://localhost:5000/calculation-requests \
  -H "Content-Type: application/json" \
  -d @test-readme-example.json \
  > actual-response.json
```

### Step 2: Check These Fields First

#### 1. calculation_outcome
```bash
# Should be "SUCCESS"
jq '.calculation_metadata.calculation_outcome' actual-response.json
```
- ? If "FAILURE": Check messages array for CRITICAL errors
- ? If "SUCCESS": Continue checking

#### 2. messages array
```bash
# Should be []
jq '.calculation_result.messages' actual-response.json
```
- ? If not empty: Check what validation failed
- ? If empty: Continue checking

#### 3. Salary after indexation
```bash
# Should be 51500
jq '.calculation_result.end_situation.situation.dossier.policies[0].salary' actual-response.json
```
- ? If 50000: Indexation not applied
- ? If null: Policy not created
- ? If 51500: Correct!

#### 4. Policy ID format
```bash
# Should be "d2222222-2222-2222-2222-222222222222-1"
jq '.calculation_result.end_situation.situation.dossier.policies[0].policy_id' actual-response.json
```
- ? If wrong: Auto-generation broken
- ? If correct: Good!

#### 5. tenant_id
```bash
# Should be "tenant001" (no dashes)
jq '.calculation_metadata.tenant_id' actual-response.json
```

---

## Common Error Patterns

### Pattern 1: CRITICAL Error in First Mutation
**Symptom:**
```json
{
  "calculation_outcome": "FAILURE",
  "messages": [
    {
      "level": "CRITICAL",
      "code": "INVALID_NAME",
      "message": "..."
    }
  ]
}
```

**Possible Causes:**
- Empty name in create_dossier
- Birth date in future
- Dossier already exists

**Fix:** Check mutation_properties values

---

### Pattern 2: Indexation Not Applied
**Symptom:**
```json
{
  "salary": 50000  // Should be 51500
}
```

**Possible Causes:**
- Property name still wrong (indexation_rate vs percentage)
- NO_MATCHING_POLICIES error
- Indexation mutation failed

**Fix:** Check apply_indexation mutation in response

---

### Pattern 3: Extra Fields in Response
**Symptom:**
```json
{
  "mutation": { ... },
  "calculation_message_indexes": null  // Should be omitted
}
```

**Possible Causes:**
- JsonIgnore not working
- Source generation issue

**Fix:** Verify JsonIgnore(Condition = WhenWritingNull) is applied

---

### Pattern 4: Wrong mutation_properties Structure
**Symptom:**
```json
{
  "mutation_properties": {
    "dossier_id": {
      "$type": "JsonElement",
      ...
    }
  }
}
```

**Possible Causes:**
- Custom converter not writing correctly
- JsonElement not being unwrapped

**Fix:** Check CalculationMutationConverter.Write()

---

## Quick Fixes

### If salary is wrong:
1. Check percentage value (should be 0.03)
2. Check if NO_MATCHING_POLICIES error occurred
3. Verify apply_indexation completed successfully

### If calculation_outcome is FAILURE:
1. Check messages array for CRITICAL errors
2. Fix the validation that's failing
3. Ensure property names match spec

### If fields are missing/extra:
1. Check JsonIgnore conditions
2. Verify null vs omitted semantics
3. Check source generation registration

---

## Full Diff Command

```bash
# Generate expected response (replace placeholders)
cat > expected-minimal.json << 'EOF'
{
  "calculation_metadata": {
    "tenant_id": "tenant001",
    "calculation_outcome": "SUCCESS"
  },
  "calculation_result": {
    "messages": [],
    "end_situation": {
      "situation": {
        "dossier": {
          "policies": [
            {
              "salary": 51500
            }
          ]
        }
      }
    }
  }
}
EOF

# Compare
jq '{
  tenant_id: .calculation_metadata.tenant_id,
  outcome: .calculation_metadata.calculation_outcome,
  messages: .calculation_result.messages,
  salary: .calculation_result.end_situation.situation.dossier.policies[0].salary
}' actual-response.json > actual-minimal.json

diff expected-minimal.json actual-minimal.json
```

---

## Need More Help?

Provide this information:
1. **Actual response** (entire JSON or specific section)
2. **Expected value** for the wrong field
3. **Actual value** for the wrong field
4. **Field path** (e.g., `.calculation_result.end_situation.situation.dossier.policies[0].salary`)

Example:
```
Field: .calculation_result.end_situation.situation.dossier.policies[0].salary
Expected: 51500
Actual: 50000
```

This helps identify the exact issue quickly!
