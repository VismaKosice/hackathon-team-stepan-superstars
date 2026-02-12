using HackatonAPI.Models;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HackatonAPI.Services;

public interface ICalculationEngine
{
    Task<CalculationResponse> ProcessCalculationAsync(CalculationRequest request);
}

public class CalculationEngine : ICalculationEngine
{
    public async Task<CalculationResponse> ProcessCalculationAsync(CalculationRequest request)
    {
        var calculationId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var messages = new List<CalculationMessage>();
        var mutationResults = new List<MutationResult>();
        
        // Initial situation (empty dossier)
        var currentSituation = new SimplifiedSituation(null);
        var initialSituation = new SituationSnapshot(
            request.CalculationInstructions.Mutations[0].ActualAt,
            currentSituation
        );
        
        // Track last successful mutation for end_situation
        CalculationMutation? lastSuccessfulMutation = null;
        SimplifiedSituation? lastSuccessfulSituation = null;
        int lastSuccessfulIndex = -1;
        
        // Process each mutation in order
        var messageIndexesBuffer = ArrayPool<int>.Shared.Rent(16);

        try
        {
            for (int i = 0; i < request.CalculationInstructions.Mutations.Length; i++)
            {
                var mutation = request.CalculationInstructions.Mutations[i];
                var messageIndexCount = 0;
                
                try
                {
                    currentSituation = await ProcessMutationAsync(
                        mutation, 
                        currentSituation, 
                        messages, 
                        messageIndexesBuffer,
                        ref messageIndexCount
                    );
                    
                    var messageIndexArray = messageIndexCount > 0 ? messageIndexesBuffer.AsSpan(0, messageIndexCount).ToArray() : null;
                    mutationResults.Add(new MutationResult(mutation, messageIndexArray));
                    
                    // Check if any CRITICAL messages were added - halt processing if so
                    if (messageIndexCount > 0)
                    {
                        var hasCritical = false;

                        for (int j = 0; j < messageIndexCount; j++)
                        {
                            if (messages[messageIndexesBuffer[j]].Level == "CRITICAL")
                            {
                                hasCritical = true;
                                break;
                            }
                        }

                        if (hasCritical) break;
                    }
                    
                    // Track last successful mutation (no CRITICAL errors)
                    lastSuccessfulMutation = mutation;
                    lastSuccessfulSituation = currentSituation;
                    lastSuccessfulIndex = i;
                }
                catch (Exception ex)
                {
                    var messageId = messages.Count;

                    messages.Add(new CalculationMessage(
                        messageId,
                        "CRITICAL",
                        "MUTATION_ERROR",
                        $"Error processing mutation: {ex.Message}"
                    ));
                    
                    messageIndexesBuffer[0] = messageId;
                    mutationResults.Add(new MutationResult(mutation, [messageId]));
                    
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(messageIndexesBuffer);
        }
        
        var endTime = DateTime.UtcNow;
        var duration = (long)(endTime - startTime).TotalMilliseconds;
        
        // Use last successful mutation for end_situation, or first mutation if none succeeded
        var endSituationMutation = lastSuccessfulMutation ?? request.CalculationInstructions.Mutations[0];
        var endSituationState = lastSuccessfulSituation ?? currentSituation;
        var endSituationIndex = lastSuccessfulIndex >= 0 ? lastSuccessfulIndex : 0;
        
        var endSituation = new SituationSnapshot(
            endSituationMutation.ActualAt,
            endSituationState,
            endSituationMutation.MutationId,
            endSituationIndex
        );
        
        var outcome = messages.Any(m => m.Level == "CRITICAL") ? "FAILURE" : "SUCCESS";
        
        return new CalculationResponse(
            new CalculationMetadata(
                calculationId,
                request.TenantId,
                startTime,
                endTime,
                duration,
                outcome
            ),
            new CalculationResult(
                messages.ToArray(),
                endSituation,
                initialSituation,
                mutationResults.ToArray()
            )
        );
    }
    
    private Task<SimplifiedSituation> ProcessMutationAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        var result = mutation.MutationDefinitionName switch
        {
            "create_dossier" => ProcessCreateDossierAsync(mutation, currentSituation, messages, messageIndexes, ref messageIndexCount),
            "add_policy" => ProcessAddPolicyAsync(mutation, currentSituation, messages, messageIndexes, ref messageIndexCount),
            "apply_indexation" => ProcessApplyIndexationAsync(mutation, currentSituation, messages, messageIndexes, ref messageIndexCount),
            "calculate_retirement_benefit" => ProcessCalculateRetirementBenefitAsync(mutation, currentSituation, messages, messageIndexes, ref messageIndexCount),
            "project_future_benefits" => ProcessProjectFutureBenefitsAsync(mutation, currentSituation, messages, messageIndexes, ref messageIndexCount),
            _ => throw new InvalidOperationException($"Unknown mutation definition: {mutation.MutationDefinitionName}")
        };
        return Task.FromResult(result);
    }
    
    private SimplifiedSituation ProcessCreateDossierAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        
        if (currentSituation.Dossier != null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_ALREADY_EXISTS",
                "Cannot create dossier: a dossier already exists"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        var dossierId = GetGuidProperty(mutation.MutationProperties, "dossier_id");
        var personId = GetGuidProperty(mutation.MutationProperties, "person_id");
        var personName = GetStringProperty(mutation.MutationProperties, "name");
        var birthDate = GetDateProperty(mutation.MutationProperties, "birth_date");
        
        // Validate name is not empty
        if (string.IsNullOrWhiteSpace(personName))
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "INVALID_NAME",
                "Name cannot be empty or whitespace"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        // Validate birth_date is not in the future
        if (birthDate > mutation.ActualAt)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "INVALID_BIRTH_DATE",
                $"Birth date cannot be in the future (birth_date: {birthDate}, actual_at: {mutation.ActualAt})"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        var person = new Person(
            personId,
            "PARTICIPANT",
            personName,
            birthDate
        );
        
        var dossier = new Dossier(
            dossierId,
            "ACTIVE",
            null,
            [person],
            []
        );
        
        return new SimplifiedSituation(dossier);
    }
    
    private SimplifiedSituation ProcessAddPolicyAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot add policy: no dossier exists"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        var schemeId = GetStringProperty(mutation.MutationProperties, "scheme_id");
        var employmentStartDate = GetDateProperty(mutation.MutationProperties, "employment_start_date");
        var salary = GetDecimalProperty(mutation.MutationProperties, "salary");
        var partTimeFactor = GetDecimalProperty(mutation.MutationProperties, "part_time_factor");
        
        // Validate salary
        if (salary < 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "INVALID_SALARY",
                "Salary must be greater than or equal to 0"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        // Validate part_time_factor
        if (partTimeFactor < 0 || partTimeFactor > 1)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "INVALID_PART_TIME_FACTOR",
                "Part-time factor must be between 0 and 1"
            ));
            messageIndexes[messageIndexCount++] = messageId;
            return currentSituation;
        }
        
        // Check for duplicate policy (same scheme_id AND employment_start_date)
        var isDuplicate = false;
        var policies = currentSituation.Dossier.Value.Policies;

        for (int i = 0; i < policies.Length; i++)
        {
            if (policies[i].SchemeId == schemeId && policies[i].EmploymentStartDate == employmentStartDate)
            {
                isDuplicate = true;
                break;
            }
        }
        
        if (isDuplicate)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "WARNING",
                "DUPLICATE_POLICY",
                $"A policy with scheme_id '{schemeId}' and employment_start_date '{employmentStartDate}' already exists"
            ));

            messageIndexes[messageIndexCount++] = messageId;
            // Continue processing - WARNING doesn't halt
        }
        
        // Auto-generate policy_id: {dossier_id}-{sequence_number}
        var policySequenceNumber = currentSituation.Dossier.Value.Policies.Length + 1;
        var policyId = $"{currentSituation.Dossier.Value.DossierId}-{policySequenceNumber}";
        
        var newPolicy = new Policy(
            policyId,
            schemeId,
            employmentStartDate,
            salary,
            partTimeFactor
        );
        
        var updatedPolicies = currentSituation.Dossier.Value.Policies.Append(newPolicy).ToArray();
        var updatedDossier = currentSituation.Dossier.Value with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private SimplifiedSituation ProcessApplyIndexationAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot apply indexation: no dossier exists"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        if (currentSituation.Dossier.Value.Policies.Length == 0)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot apply indexation: dossier has no policies"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        var percentage = GetDecimalProperty(mutation.MutationProperties, "percentage");
        var schemeIdFilter = GetOptionalStringProperty(mutation.MutationProperties, "scheme_id");
        var effectiveBeforeFilter = GetOptionalDateProperty(mutation.MutationProperties, "effective_before");
        
        var hasFilters = schemeIdFilter != null || effectiveBeforeFilter != null;
        var policies = currentSituation.Dossier.Value.Policies;
        
        // Count matching policies if filters are present
        var matchCount = 0;

        if (hasFilters)
        {
            for (int i = 0; i < policies.Length; i++)
            {
                var matches = true;

                if (schemeIdFilter != null && policies[i].SchemeId != schemeIdFilter)
                {
                    matches = false;
                }
                else if (effectiveBeforeFilter != null && policies[i].EmploymentStartDate >= effectiveBeforeFilter.Value)
                { 
                    matches = false;
                }

                if (matches)
                {
                    matchCount++;
                }
            }
            
            if (matchCount == 0)
            {
                var messageId = messages.Count;

                messages.Add(new CalculationMessage(
                    messageId,
                    "WARNING",
                    "NO_MATCHING_POLICIES",
                    "No policies match the specified filter criteria"
                ));

                messageIndexes[messageIndexCount++] = messageId;

                return currentSituation;
            }
        }
        
        // Apply indexation with clamping
        var updatedPolicies = new Policy[policies.Length];

        for (int i = 0; i < policies.Length; i++)
        {
            var p = policies[i];
            var shouldUpdate = !hasFilters;

            if (hasFilters)
            {
                shouldUpdate = true;

                if (schemeIdFilter != null && p.SchemeId != schemeIdFilter)
                {
                    shouldUpdate = false;
                }
                else if (effectiveBeforeFilter != null && p.EmploymentStartDate >= effectiveBeforeFilter.Value)
                {
                    shouldUpdate = false;
                }
            }
            
            if (!shouldUpdate)
            {
                updatedPolicies[i] = p;
                continue;
            }
            
            var newSalary = p.Salary * (1 + percentage);
            decimal? newAttainablePension = p.AttainablePension.HasValue 
                ? p.AttainablePension.Value * (1 + percentage) 
                : null;
            
            // Check for negative salary after indexation
            if (newSalary < 0)
            {
                var messageId = messages.Count;

                messages.Add(new CalculationMessage(
                    messageId,
                    "WARNING",
                    "NEGATIVE_SALARY_CLAMPED",
                    $"Salary for policy {p.PolicyId} would be negative after indexation. Clamped to 0."
                ));

                messageIndexes[messageIndexCount++] = messageId;
                newSalary = 0;
            }
            
            // Clamp attainable pension if necessary
            if (newAttainablePension.HasValue && newAttainablePension.Value < 0)
            {
                newAttainablePension = 0;
            }
            
            updatedPolicies[i] = p with 
            { 
                Salary = newSalary,
                AttainablePension = newAttainablePension
            };
        }
        
        var updatedDossier = currentSituation.Dossier.Value with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private readonly struct PolicyYearsData(Policy policy, decimal years, decimal effectiveSalary)
    {
        public readonly Policy Policy = policy;
        public readonly decimal Years = years;
        public readonly decimal EffectiveSalary = effectiveSalary;
    }

    private SimplifiedSituation ProcessCalculateRetirementBenefitAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot calculate retirement benefit: no dossier exists"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        if (currentSituation.Dossier.Value.Policies.Length == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot calculate retirement benefit: dossier has no policies"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        var retirementDate = GetDateProperty(mutation.MutationProperties, "retirement_date");
        const decimal accrualRate = 0.02m; // Hardcoded default accrual rate
        
        // Get participant's birth date for eligibility check
        Person? participant = null;
        var persons = currentSituation.Dossier.Value.Persons;

        for (int i = 0; i < persons.Length; i++)
        {
            if (persons[i].Role == "PARTICIPANT")
            {
                participant = persons[i];
                break;
            }
        }
        
        if (participant == null)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_PARTICIPANT",
                "Cannot calculate retirement benefit: no participant found"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        // Calculate years of service per policy and check for warnings
        var policies = currentSituation.Dossier.Value.Policies;
        var policyData = new PolicyYearsData[policies.Length];
        decimal totalYears = 0;

        for (int i = 0; i < policies.Length; i++)
        {
            var p = policies[i];
            decimal years;

            if (retirementDate < p.EmploymentStartDate)
            {
                // Retirement before employment - produce warning
                var messageId = messages.Count;
                messages.Add(new CalculationMessage(
                    messageId,
                    "WARNING",
                    "RETIREMENT_BEFORE_EMPLOYMENT",
                    $"Retirement date is before employment start date for policy {p.PolicyId}"
                ));
                messageIndexes[messageIndexCount++] = messageId;
                years = 0;
            }
            else
            {
                var days = (retirementDate.ToDateTime(TimeOnly.MinValue) - p.EmploymentStartDate.ToDateTime(TimeOnly.MinValue)).TotalDays;
                years = (decimal)Math.Max(0, days / 365.25);
            }
            
            var effectiveSalary = p.Salary * p.PartTimeFactor;
            policyData[i] = new PolicyYearsData(p, years, effectiveSalary);
            totalYears += years;
        }
        
        // Check eligibility: age >= 65 OR total years >= 40
        var ageAtRetirement = (retirementDate.ToDateTime(TimeOnly.MinValue) - participant.Value.BirthDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.25;
        var isEligible = ageAtRetirement >= 65 || totalYears >= 40;
        
        if (!isEligible)
        {
            var messageId = messages.Count;

            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NOT_ELIGIBLE",
                $"Participant is not eligible for retirement (age: {ageAtRetirement:F1}, years of service: {totalYears:F1}). Must be 65+ years old OR have 40+ years of service."
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        // Calculate weighted average salary
        decimal weightedAvgSalary = 0;

        if (totalYears > 0)
        {
            decimal weightedSum = 0;

            for (int i = 0; i < policyData.Length; i++)
            {
                weightedSum += policyData[i].EffectiveSalary * policyData[i].Years;
            }

            weightedAvgSalary = weightedSum / totalYears;
        }
        
        // Calculate annual pension
        var annualPension = weightedAvgSalary * totalYears * accrualRate;
        
        // Distribute proportionally to policies
        var updatedPolicies = new Policy[policyData.Length];

        for (int i = 0; i < policyData.Length; i++)
        {
            var policyPension = totalYears > 0 
                ? annualPension * (policyData[i].Years / totalYears) 
                : 0;
            updatedPolicies[i] = policyData[i].Policy with { AttainablePension = policyPension };
        }
        
        var updatedDossier = currentSituation.Dossier.Value with 
        { 
            Policies = updatedPolicies,
            Status = "RETIRED",
            RetirementDate = retirementDate
        };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private SimplifiedSituation ProcessProjectFutureBenefitsAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        int[] messageIndexes,
        ref int messageIndexCount)
    {
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_DOSSIER",
                "Cannot project future benefits: no dossier exists"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        if (currentSituation.Dossier.Value.Policies.Length == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot project future benefits: dossier has no policies"
            ));

            messageIndexes[messageIndexCount++] = messageId;

            return currentSituation;
        }
        
        var projectionStartDate = GetDateProperty(mutation.MutationProperties, "projection_start_date");
        var projectionEndDate = GetDateProperty(mutation.MutationProperties, "projection_end_date");
        var projectionIntervalMonths = GetIntProperty(mutation.MutationProperties, "projection_interval_months");
        
        const decimal accrualRate = 0.02m;
        
        // Count projection dates
        var projectionCount = 0;
        var tempDate = projectionStartDate;

        while (tempDate <= projectionEndDate)
        {
            projectionCount++;
            tempDate = tempDate.AddMonths(projectionIntervalMonths);
        }
        
        var policies = currentSituation.Dossier.Value.Policies;
        var updatedPolicies = new Policy[policies.Length];
        
        // For each policy, calculate projections
        for (int policyIdx = 0; policyIdx < policies.Length; policyIdx++)
        {
            var policy = policies[policyIdx];
            var projections = new Projection[projectionCount];
            var projIdx = 0;
            var currentDate = projectionStartDate;
            
            while (currentDate <= projectionEndDate)
            {
                // Calculate this policy's contribution for the projection
                var projectedPension = CalculatePolicyProjection(
                    policies,
                    policy,
                    currentDate,
                    accrualRate);
                
                projections[projIdx++] = new Projection(currentDate, projectedPension);
                currentDate = currentDate.AddMonths(projectionIntervalMonths);
            }
            
            updatedPolicies[policyIdx] = policy with { Projections = projections };
        }
        
        var updatedDossier = currentSituation.Dossier.Value with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private static decimal CalculatePolicyProjection(
        Policy[] allPolicies,
        Policy targetPolicy,
        DateOnly projectionDate,
        decimal accrualRate)
    {
        // Calculate years of service and effective salary for each policy
        var policyData = new PolicyYearsData[allPolicies.Length];
        decimal totalYears = 0;

        for (int i = 0; i < allPolicies.Length; i++)
        {
            var p = allPolicies[i];
            var days = (projectionDate.ToDateTime(TimeOnly.MinValue) - p.EmploymentStartDate.ToDateTime(TimeOnly.MinValue)).TotalDays;
            var years = (decimal)Math.Max(0, days / 365.25);
            var effectiveSalary = p.Salary * p.PartTimeFactor;
            
            policyData[i] = new PolicyYearsData(p, years, effectiveSalary);
            totalYears += years;
        }
        
        if (totalYears == 0)
        {
            return 0;
        }
        
        // Calculate weighted average salary
        decimal weightedSum = 0;

        for (int i = 0; i < policyData.Length; i++)
        {
            weightedSum += policyData[i].EffectiveSalary * policyData[i].Years;
        }

        var weightedAvgSalary = weightedSum / totalYears;
        
        // Calculate total annual pension
        var annualPension = weightedAvgSalary * totalYears * accrualRate;
        
        // Find the target policy's share
        decimal targetYears = 0;

        for (int i = 0; i < policyData.Length; i++)
        {
            if (policyData[i].Policy.PolicyId == targetPolicy.PolicyId)
            {
                targetYears = policyData[i].Years;
                break;
            }
        }
        
        var policyPension = annualPension * (targetYears / totalYears);
        return policyPension;
    }
    
    private static Guid GetGuidProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                return Guid.Parse(element.GetString()!);
            }
            return Guid.Parse(value.ToString()!);
        }
        throw new InvalidOperationException($"Property '{key}' not found");
    }
    
    private static string GetStringProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString()!;
            }
            return value.ToString()!;
        }
        throw new InvalidOperationException($"Property '{key}' not found");
    }
    
    private static DateOnly GetDateProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                return DateOnly.Parse(element.GetString()!);
            }

            return DateOnly.Parse(value.ToString()!);
        }

        throw new InvalidOperationException($"Property '{key}' not found");
    }
    
    private static decimal GetDecimalProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.Number 
                    ? element.GetDecimal() 
                    : decimal.Parse(element.GetString()!);
            }
            return Convert.ToDecimal(value);
        }
        throw new InvalidOperationException($"Property '{key}' not found");
    }
    
    private static int GetIntProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.Number 
                    ? element.GetInt32() 
                    : int.Parse(element.GetString()!);
            }
            return Convert.ToInt32(value);
        }
        throw new InvalidOperationException($"Property '{key}' not found");
    }
    
    private static T[] GetArrayProperty<T>(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                if (typeof(T) == typeof(DateOnly))
                {
                    return element.EnumerateArray()
                        .Select(e => (T)(object)DateOnly.Parse(e.GetString()!))
                        .ToArray();
                }
            }
        }
        throw new InvalidOperationException($"Property '{key}' not found or invalid type");
    }
    
    private static string? GetOptionalStringProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
            return value?.ToString();
        }
        return null;
    }
    
    private static DateOnly? GetOptionalDateProperty(Dictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                return DateOnly.Parse(element.GetString()!);
            }
            return DateOnly.Parse(value.ToString()!);
        }
        return null;
    }
}
