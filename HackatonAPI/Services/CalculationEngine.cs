using HackatonAPI.Models;
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
        for (int i = 0; i < request.CalculationInstructions.Mutations.Length; i++)
        {
            var mutation = request.CalculationInstructions.Mutations[i];
            var messageIndexes = new List<int>();
            
            try
            {
                currentSituation = await ProcessMutationAsync(
                    mutation, 
                    currentSituation, 
                    messages, 
                    messageIndexes
                );
                
                mutationResults.Add(new MutationResult(
                    mutation,
                    messageIndexes.Count > 0 ? messageIndexes.ToArray() : null
                ));
                
                // Check if any CRITICAL messages were added - halt processing if so
                if (messageIndexes.Any() && messages.Any(m => messageIndexes.Contains(m.Id) && m.Level == "CRITICAL"))
                {
                    break;
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
                messageIndexes.Add(messageId);
                
                mutationResults.Add(new MutationResult(
                    mutation,
                    messageIndexes.Count > 0 ? messageIndexes.ToArray() : null
                ));
                
                break;
            }
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
    
    private async Task<SimplifiedSituation> ProcessMutationAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        return mutation.MutationDefinitionName switch
        {
            "create_dossier" => await ProcessCreateDossierAsync(mutation, currentSituation, messages, messageIndexes),
            "add_policy" => await ProcessAddPolicyAsync(mutation, currentSituation, messages, messageIndexes),
            "apply_indexation" => await ProcessApplyIndexationAsync(mutation, currentSituation, messages, messageIndexes),
            "calculate_retirement_benefit" => await ProcessCalculateRetirementBenefitAsync(mutation, currentSituation, messages, messageIndexes),
            "project_future_benefits" => await ProcessProjectFutureBenefitsAsync(mutation, currentSituation, messages, messageIndexes),
            _ => throw new InvalidOperationException($"Unknown mutation definition: {mutation.MutationDefinitionName}")
        };
    }
    
    private async Task<SimplifiedSituation> ProcessCreateDossierAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        await Task.CompletedTask;
        
        if (currentSituation.Dossier != null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_ALREADY_EXISTS",
                "Cannot create dossier: a dossier already exists"
            ));
            messageIndexes.Add(messageId);
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
            messageIndexes.Add(messageId);
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
            messageIndexes.Add(messageId);
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
    
    private async Task<SimplifiedSituation> ProcessAddPolicyAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        await Task.CompletedTask;
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot add policy: no dossier exists"
            ));
            messageIndexes.Add(messageId);
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
            messageIndexes.Add(messageId);
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
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        // Check for duplicate policy (same scheme_id AND employment_start_date)
        var isDuplicate = currentSituation.Dossier.Policies.Any(p =>
            p.SchemeId == schemeId && 
            p.EmploymentStartDate == employmentStartDate);
        
        if (isDuplicate)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "WARNING",
                "DUPLICATE_POLICY",
                $"A policy with scheme_id '{schemeId}' and employment_start_date '{employmentStartDate}' already exists"
            ));
            messageIndexes.Add(messageId);
            // Continue processing - WARNING doesn't halt
        }
        
        // Auto-generate policy_id: {dossier_id}-{sequence_number}
        var policySequenceNumber = currentSituation.Dossier.Policies.Length + 1;
        var policyId = $"{currentSituation.Dossier.DossierId}-{policySequenceNumber}";
        
        var newPolicy = new Policy(
            policyId,
            schemeId,
            employmentStartDate,
            salary,
            partTimeFactor
        );
        
        var updatedPolicies = currentSituation.Dossier.Policies.Append(newPolicy).ToArray();
        var updatedDossier = currentSituation.Dossier with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private async Task<SimplifiedSituation> ProcessApplyIndexationAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        await Task.CompletedTask;
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot apply indexation: no dossier exists"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        if (currentSituation.Dossier.Policies.Length == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot apply indexation: dossier has no policies"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        var percentage = GetDecimalProperty(mutation.MutationProperties, "percentage");
        var schemeIdFilter = GetOptionalStringProperty(mutation.MutationProperties, "scheme_id");
        var effectiveBeforeFilter = GetOptionalDateProperty(mutation.MutationProperties, "effective_before");
        
        // Filter policies based on criteria
        var policiesToUpdate = currentSituation.Dossier.Policies.AsEnumerable();
        
        if (schemeIdFilter != null)
        {
            policiesToUpdate = policiesToUpdate.Where(p => p.SchemeId == schemeIdFilter);
        }
        
        if (effectiveBeforeFilter != null)
        {
            policiesToUpdate = policiesToUpdate.Where(p => p.EmploymentStartDate < effectiveBeforeFilter.Value);
        }
        
        var matchingPolicies = policiesToUpdate.ToList();
        
        // Check if filters were provided but no policies match
        if ((schemeIdFilter != null || effectiveBeforeFilter != null) && matchingPolicies.Count == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "WARNING",
                "NO_MATCHING_POLICIES",
                "No policies match the specified filter criteria"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        // Create a dictionary for quick lookup of policies to update
        var policiesToUpdateSet = new HashSet<string>(matchingPolicies.Select(p => p.PolicyId));
        
        // Apply indexation with clamping
        var updatedPolicies = currentSituation.Dossier.Policies
            .Select(p =>
            {
                if (!policiesToUpdateSet.Contains(p.PolicyId))
                {
                    return p; // Don't update this policy
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
                    messageIndexes.Add(messageId);
                    newSalary = 0;
                }
                
                
                // Clamp attainable pension if necessary
                if (newAttainablePension.HasValue && newAttainablePension.Value < 0)
                {
                    newAttainablePension = 0;
                }
                
                return p with 
                { 
                    Salary = newSalary,
                    AttainablePension = newAttainablePension
                };
            })
            .ToArray();
        
        var updatedDossier = currentSituation.Dossier with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private async Task<SimplifiedSituation> ProcessCalculateRetirementBenefitAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        await Task.CompletedTask;
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "DOSSIER_NOT_FOUND",
                "Cannot calculate retirement benefit: no dossier exists"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        if (currentSituation.Dossier.Policies.Length == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot calculate retirement benefit: dossier has no policies"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        var retirementDate = GetDateProperty(mutation.MutationProperties, "retirement_date");
        const decimal accrualRate = 0.02m; // Hardcoded default accrual rate
        
        // Get participant's birth date for eligibility check
        var participant = currentSituation.Dossier.Persons.FirstOrDefault(p => p.Role == "PARTICIPANT");
        if (participant == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_PARTICIPANT",
                "Cannot calculate retirement benefit: no participant found"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        // Calculate years of service per policy and check for warnings
        var policyData = currentSituation.Dossier.Policies
            .Select(p =>
            {
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
                    messageIndexes.Add(messageId);
                    years = 0;
                }
                else
                {
                    var days = (retirementDate.ToDateTime(TimeOnly.MinValue) - p.EmploymentStartDate.ToDateTime(TimeOnly.MinValue)).TotalDays;
                    years = (decimal)(Math.Max(0, days / 365.25));
                }
                
                var effectiveSalary = p.Salary * p.PartTimeFactor;
                
                return new
                {
                    Policy = p,
                    Years = years,
                    EffectiveSalary = effectiveSalary
                };
            })
            .ToArray();
        
        var totalYears = policyData.Sum(pd => pd.Years);
        
        // Check eligibility: age >= 65 OR total years >= 40
        var ageAtRetirement = (retirementDate.ToDateTime(TimeOnly.MinValue) - participant.BirthDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.25;
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
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        // Calculate weighted average salary
        decimal weightedAvgSalary;
        if (totalYears > 0)
        {
            var weightedSum = policyData.Sum(pd => pd.EffectiveSalary * pd.Years);
            weightedAvgSalary = weightedSum / totalYears;
        }
        else
        {
            weightedAvgSalary = 0;
        }
        
        // Calculate annual pension
        var annualPension = weightedAvgSalary * totalYears * accrualRate;
        
        // Distribute proportionally to policies
        var updatedPolicies = policyData
            .Select(pd =>
            {
                var policyPension = totalYears > 0 
                    ? annualPension * (pd.Years / totalYears) 
                    : 0;
                return pd.Policy with { AttainablePension = policyPension };
            })
            .ToArray();
        
        var updatedDossier = currentSituation.Dossier with 
        { 
            Policies = updatedPolicies,
            Status = "RETIRED",
            RetirementDate = retirementDate
        };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private async Task<SimplifiedSituation> ProcessProjectFutureBenefitsAsync(
        CalculationMutation mutation,
        SimplifiedSituation currentSituation,
        List<CalculationMessage> messages,
        List<int> messageIndexes)
    {
        await Task.CompletedTask;
        
        if (currentSituation.Dossier == null)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_DOSSIER",
                "Cannot project future benefits: no dossier exists"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        if (currentSituation.Dossier.Policies.Length == 0)
        {
            var messageId = messages.Count;
            messages.Add(new CalculationMessage(
                messageId,
                "CRITICAL",
                "NO_POLICIES",
                "Cannot project future benefits: dossier has no policies"
            ));
            messageIndexes.Add(messageId);
            return currentSituation;
        }
        
        var projectionStartDate = GetDateProperty(mutation.MutationProperties, "projection_start_date");
        var projectionEndDate = GetDateProperty(mutation.MutationProperties, "projection_end_date");
        var projectionIntervalMonths = GetIntProperty(mutation.MutationProperties, "projection_interval_months");
        
        const decimal accrualRate = 0.02m;
        
        // Generate projection dates
        var projectionDates = new List<DateOnly>();
        var currentDate = projectionStartDate;
        while (currentDate <= projectionEndDate)
        {
            projectionDates.Add(currentDate);
            currentDate = currentDate.AddMonths(projectionIntervalMonths);
        }
        
        // For each projection date, calculate pension as if retiring on that date (no eligibility check)
        var updatedPolicies = currentSituation.Dossier.Policies
            .Select(policy =>
            {
                var projections = projectionDates
                    .Select(projectionDate =>
                    {
                        // Calculate this policy's contribution for the projection
                        var projectedPension = CalculatePolicyProjection(
                            currentSituation.Dossier.Policies,
                            policy,
                            projectionDate,
                            accrualRate);
                        
                        return new Projection(projectionDate, projectedPension);
                    })
                    .ToArray();
                
                return policy with { Projections = projections };
            })
            .ToArray();
        
        var updatedDossier = currentSituation.Dossier with { Policies = updatedPolicies };
        
        return new SimplifiedSituation(updatedDossier);
    }
    
    private static decimal CalculatePolicyProjection(
        Policy[] allPolicies,
        Policy targetPolicy,
        DateOnly projectionDate,
        decimal accrualRate)
    {
        // Calculate years of service and effective salary for each policy
        var policyData = allPolicies
            .Select(p =>
            {
                var days = (projectionDate.ToDateTime(TimeOnly.MinValue) - p.EmploymentStartDate.ToDateTime(TimeOnly.MinValue)).TotalDays;
                var years = (decimal)Math.Max(0, days / 365.25);
                var effectiveSalary = p.Salary * p.PartTimeFactor;
                
                return new
                {
                    Policy = p,
                    Years = years,
                    EffectiveSalary = effectiveSalary
                };
            })
            .ToArray();
        
        var totalYears = policyData.Sum(pd => pd.Years);
        
        if (totalYears == 0)
        {
            return 0;
        }
        
        // Calculate weighted average salary
        var weightedSum = policyData.Sum(pd => pd.EffectiveSalary * pd.Years);
        var weightedAvgSalary = weightedSum / totalYears;
        
        // Calculate total annual pension
        var annualPension = weightedAvgSalary * totalYears * accrualRate;
        
        // Find the target policy's share
        var targetPolicyData = policyData.First(pd => pd.Policy.PolicyId == targetPolicy.PolicyId);
        var policyPension = annualPension * (targetPolicyData.Years / totalYears);
        
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
