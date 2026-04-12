using System.Text.Json;
using System.Text.Json.Serialization;
using CatConfLang.TestRunner.Abstractions;
using CatConfLang.TestRunner.Execution;
using CatConfLang.TestRunner.TestData;

namespace CatConfLang.TestRunner.Reporting;

/// <summary>
/// Writes a test-results-format JSON file conforming to
/// https://catconflang.com/test-results-format/ (v1.1.0).
///
/// The format intentionally excludes diffs, expected/actual values, aggregate
/// counts, and environment metadata — consumers derive those by aggregating
/// the flat <c>tests</c> array.
/// </summary>
public static class JsonResultsWriter
{
  private const string SchemaUrl =
      "https://raw.githubusercontent.com/CatConfLang/ccl-test-data/v1.1.0/schemas/test-results-format.json";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
  };

  public static void Write(
      string path,
      ICclImplementation impl,
      ImplementationConfig config,
      string testDataPath,
      IReadOnlyList<TestOutcome> outcomes)
  {
    var document = new ResultsDocument
    {
      Schema = SchemaUrl,
      GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
      Implementation = new ImplementationBlock
      {
        Name = impl.Name,
        Version = impl.ImplementationVersion,
        Language = "csharp",
        Variant = config.Variants.FirstOrDefault() ?? "default",
        ImplementedFunctions = config.Functions.OrderBy(f => f, StringComparer.Ordinal).ToList(),
      },
      TestSuite = new TestSuiteBlock
      {
        Version = ReadSuiteVersion(testDataPath),
        TotalTests = outcomes.Count,
      },
      Tests = outcomes.Select(ToTestRecord).ToList(),
    };

    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
      Directory.CreateDirectory(directory);

    File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
  }

  private static TestRecord ToTestRecord(TestOutcome outcome)
  {
    var test = outcome.Test;
    return new TestRecord
    {
      Name = test.Name,
      Validation = test.Validation,
      Features = test.Features,
      Behaviors = test.Behaviors,
      Variants = test.Variants,
      Outcome = outcome.Kind switch
      {
        OutcomeKind.Passed => "pass",
        OutcomeKind.Failed => "fail",
        OutcomeKind.Skipped => "skip",
        OutcomeKind.Todo => "todo",
        _ => throw new InvalidOperationException($"Unknown outcome kind: {outcome.Kind}"),
      },
      Reason = outcome.Kind is OutcomeKind.Skipped or OutcomeKind.Todo ? outcome.Reason : null,
      Error = outcome.Kind == OutcomeKind.Failed ? outcome.Reason : null,
      DurationMs = outcome.Duration > TimeSpan.Zero ? outcome.Duration.TotalMilliseconds : null,
    };
  }

  private static string? ReadSuiteVersion(string testDataPath)
  {
    foreach (var candidate in new[] { "VERSION", "../VERSION" })
    {
      var path = Path.Combine(testDataPath, candidate);
      if (File.Exists(path))
        return File.ReadAllText(path).Trim();
    }
    return null;
  }

  private sealed class ResultsDocument
  {
    [JsonPropertyName("$schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("generatedAt")] public string GeneratedAt { get; set; } = string.Empty;
    [JsonPropertyName("implementation")] public ImplementationBlock Implementation { get; set; } = new();
    [JsonPropertyName("testSuite")] public TestSuiteBlock TestSuite { get; set; } = new();
    [JsonPropertyName("tests")] public List<TestRecord> Tests { get; set; } = [];
  }

  private sealed class ImplementationBlock
  {
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("variant")] public string Variant { get; set; } = string.Empty;
    [JsonPropertyName("implementedFunctions")] public List<string> ImplementedFunctions { get; set; } = [];
  }

  private sealed class TestSuiteBlock
  {
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("totalTests")] public int TotalTests { get; set; }
  }

  private sealed class TestRecord
  {
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("validation")] public string Validation { get; set; } = string.Empty;
    [JsonPropertyName("features")] public List<string> Features { get; set; } = [];
    [JsonPropertyName("behaviors")] public List<string> Behaviors { get; set; } = [];
    [JsonPropertyName("variants")] public List<string> Variants { get; set; } = [];
    [JsonPropertyName("outcome")] public string Outcome { get; set; } = string.Empty;
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("durationMs")] public double? DurationMs { get; set; }
  }
}
