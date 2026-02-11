using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using HackatonAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackatonAPI.IntegrationTests;

/// <summary>
/// Data-driven tests using JSON test case files from the test-cases folder
/// </summary>
public class JsonTestCaseTests : IntegrationTestBase
{
    private const string TestCasesFolder = "test-cases";

    public JsonTestCaseTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    public static IEnumerable<object[]> GetTestCaseFiles()
    {
        var testCasesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", TestCasesFolder);
        
        if (!Directory.Exists(testCasesPath))
        {
            // Try relative to solution root
            testCasesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", TestCasesFolder);
        }

        if (!Directory.Exists(testCasesPath))
        {
            yield break;
        }

        var jsonFiles = Directory.GetFiles(testCasesPath, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith("README.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in jsonFiles)
        {
            yield return new object[] { file };
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCaseFiles))]
    public async Task JsonTestCase_ShouldMatchExpectedResponse(string testCaseFilePath)
    {
        // Arrange - Load and parse the test case
        var testCaseJson = await File.ReadAllTextAsync(testCaseFilePath);
        var testCase = JsonDocument.Parse(testCaseJson);
        var root = testCase.RootElement;

        var testId = root.GetProperty("id").GetString();
        var testName = root.GetProperty("name").GetString();
        var description = root.GetProperty("description").GetString();

        // Parse request
        var requestJson = root.GetProperty("request").GetRawText();
        var request = JsonSerializer.Deserialize<CalculationRequest>(requestJson, JsonOptions);
        request.Should().NotBeNull($"Test case {testId} should have a valid request");

        // Parse expected values
        var expected = root.GetProperty("expected");
        var expectedHttpStatus = expected.GetProperty("http_status").GetInt32();
        var expectedOutcome = expected.GetProperty("calculation_outcome").GetString();
        var expectedMessageCount = expected.GetProperty("message_count").GetInt32();
        var expectedMutationsProcessedCount = expected.GetProperty("mutations_processed_count").GetInt32();

        // Act
        var httpResponse = await Client.PostAsJsonAsync("/calculation-requests", request, JsonOptions);

        // Assert - HTTP Status
        ((int)httpResponse.StatusCode).Should().Be(expectedHttpStatus, 
            $"Test {testId} ({testName}): HTTP status should match");

        if (!httpResponse.IsSuccessStatusCode)
        {
            return; // For non-200 responses, we can't parse the calculation response
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<CalculationResponse>(JsonOptions);
        response.Should().NotBeNull($"Test {testId} ({testName}): Response should be parseable");

        // Assert - Calculation outcome
        response!.CalculationMetadata.CalculationOutcome.Should().Be(expectedOutcome,
            $"Test {testId} ({testName}): {description}");

        // Assert - Message count
        response.CalculationResult.Messages.Count().Should().Be(expectedMessageCount,
            $"Test {testId} ({testName}): Message count should match");

        // Assert - Expected messages (if specified)
        if (expected.TryGetProperty("messages", out var expectedMessagesElement))
        {
            var expectedMessages = expectedMessagesElement.EnumerateArray().ToList();
            
            for (int i = 0; i < expectedMessages.Count; i++)
            {
                var expectedMsg = expectedMessages[i];
                var expectedLevel = expectedMsg.GetProperty("level").GetString();
                var expectedCode = expectedMsg.GetProperty("code").GetString();

                response.CalculationResult.Messages.Should().Contain(m => 
                    m.Level == expectedLevel && m.Code == expectedCode,
                    $"Test {testId} ({testName}): Should contain message with level={expectedLevel} and code={expectedCode}");
            }
        }

        // Assert - Mutations processed count
        response.CalculationResult.Mutations.Count().Should().Be(expectedMutationsProcessedCount,
            $"Test {testId} ({testName}): Mutations processed count should match");

        // Assert - End situation metadata
        if (expected.TryGetProperty("end_situation_mutation_id", out var mutationIdElement))
        {
            var expectedMutationId = Guid.Parse(mutationIdElement.GetString()!);
            response.CalculationResult.EndSituation.MutationId.Should().Be(expectedMutationId,
                $"Test {testId} ({testName}): End situation mutation_id should match");
        }

        if (expected.TryGetProperty("end_situation_mutation_index", out var mutationIndexElement))
        {
            var expectedMutationIndex = mutationIndexElement.GetInt32();
            response.CalculationResult.EndSituation.MutationIndex.Should().Be(expectedMutationIndex,
                $"Test {testId} ({testName}): End situation mutation_index should match");
        }

        if (expected.TryGetProperty("end_situation_actual_at", out var actualAtElement))
        {
            var expectedActualAt = DateOnly.Parse(actualAtElement.GetString()!);
            response.CalculationResult.EndSituation.ActualAt.Should().Be(expectedActualAt,
                $"Test {testId} ({testName}): End situation actual_at should match");
        }

        // Assert - End situation (detailed comparison)
        if (expected.TryGetProperty("end_situation", out var expectedSituationElement))
        {
            var expectedSituationJson = expectedSituationElement.GetRawText();
            var actualSituationJson = JsonSerializer.Serialize(
                response.CalculationResult.EndSituation.Situation, 
                JsonOptions);

            // Parse both as JsonNode for flexible comparison
            var expectedNode = JsonNode.Parse(expectedSituationJson);
            var actualNode = JsonNode.Parse(actualSituationJson);

            CompareJsonNodes(expectedNode, actualNode, testId!, testName!, "end_situation");
        }
    }

    private void CompareJsonNodes(JsonNode? expected, JsonNode? actual, string testId, string testName, string path)
    {
        if (expected == null && actual == null) return;

        expected.Should().NotBeNull($"Test {testId} ({testName}): Expected value at {path} should not be null");
        actual.Should().NotBeNull($"Test {testId} ({testName}): Actual value at {path} should not be null");

        if (expected is JsonObject expectedObj && actual is JsonObject actualObj)
        {
            // Compare objects
            foreach (var prop in expectedObj)
            {
                var propPath = $"{path}.{prop.Key}";
                
                actualObj.Should().ContainKey(prop.Key, 
                    $"Test {testId} ({testName}): Actual should have property {propPath}");

                CompareJsonNodes(prop.Value, actualObj[prop.Key], testId, testName, propPath);
            }
        }
        else if (expected is JsonArray expectedArr && actual is JsonArray actualArr)
        {
            // Compare arrays
            actualArr.Count.Should().Be(expectedArr.Count,
                $"Test {testId} ({testName}): Array length at {path} should match");

            for (int i = 0; i < expectedArr.Count; i++)
            {
                CompareJsonNodes(expectedArr[i], actualArr[i], testId, testName, $"{path}[{i}]");
            }
        }
        else if (expected is JsonValue expectedVal && actual is JsonValue actualVal)
        {
            // Compare values - handle numeric tolerance
            if (expectedVal.TryGetValue<decimal>(out var expectedDecimal) && 
                actualVal.TryGetValue<decimal>(out var actualDecimal))
            {
                actualDecimal.Should().BeApproximately(expectedDecimal, 0.01m,
                    $"Test {testId} ({testName}): Numeric value at {path} should match (within tolerance)");
            }
            else if (expectedVal.TryGetValue<double>(out var expectedDouble) && 
                     actualVal.TryGetValue<double>(out var actualDouble))
            {
                actualDouble.Should().BeApproximately(expectedDouble, 0.01,
                    $"Test {testId} ({testName}): Numeric value at {path} should match (within tolerance)");
            }
            else
            {
                // String or other value comparison
                var expectedStr = expectedVal.ToJsonString();
                var actualStr = actualVal.ToJsonString();
                actualStr.Should().Be(expectedStr,
                    $"Test {testId} ({testName}): Value at {path} should match");
            }
        }
    }
}
