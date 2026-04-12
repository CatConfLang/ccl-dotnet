using System.Text.Json.Serialization;

namespace CatConfLang.TestRunner.TestData;

/// <summary>
/// Root object for a CCL test suite JSON file.
/// </summary>
public sealed class TestSuiteFile
{
  [JsonPropertyName("$schema")]
  public string? Schema { get; set; }

  [JsonPropertyName("tests")]
  public List<CclTestCase> Tests { get; set; } = [];
}

/// <summary>
/// A single test case from the CCL test suite.
/// </summary>
public sealed class CclTestCase
{
  [JsonPropertyName("name")]
  public string Name { get; set; } = string.Empty;

  [JsonPropertyName("inputs")]
  public List<string> Inputs { get; set; } = [];

  [JsonPropertyName("validation")]
  public string Validation { get; set; } = string.Empty;

  [JsonPropertyName("expected")]
  public ExpectedResult Expected { get; set; } = new();

  [JsonPropertyName("args")]
  public List<string>? Args { get; set; }

  [JsonPropertyName("predicate")]
  public FilterPredicate? Predicate { get; set; }

  [JsonPropertyName("functions")]
  public List<string> Functions { get; set; } = [];

  [JsonPropertyName("behaviors")]
  public List<string> Behaviors { get; set; } = [];

  [JsonPropertyName("variants")]
  public List<string> Variants { get; set; } = [];

  [JsonPropertyName("features")]
  public List<string> Features { get; set; } = [];

  [JsonPropertyName("conflicts")]
  public ConflictSpec? Conflicts { get; set; }

  [JsonPropertyName("requires")]
  public List<string>? Requires { get; set; }

  [JsonPropertyName("source_test")]
  public string? SourceTest { get; set; }

  [JsonPropertyName("expect_error")]
  public bool ExpectError { get; set; }

  [JsonPropertyName("error_type")]
  public string? ErrorType { get; set; }

  public override string ToString() => Name;
}

/// <summary>
/// Expected result for a test case.
/// </summary>
public sealed class ExpectedResult
{
  [JsonPropertyName("count")]
  public int Count { get; set; }

  [JsonPropertyName("entries")]
  public List<ExpectedEntry>? Entries { get; set; }

  [JsonPropertyName("object")]
  public System.Text.Json.JsonElement? Object { get; set; }

  [JsonPropertyName("value")]
  public System.Text.Json.JsonElement? Value { get; set; }

  [JsonPropertyName("list")]
  public System.Text.Json.JsonElement? List { get; set; }

  [JsonPropertyName("text")]
  public string? Text { get; set; }

  [JsonPropertyName("boolean")]
  public bool? Boolean { get; set; }

  [JsonPropertyName("error")]
  public bool Error { get; set; }
}

/// <summary>
/// An expected key-value entry from a parse result.
/// </summary>
public sealed class ExpectedEntry
{
  [JsonPropertyName("key")]
  public string Key { get; set; } = string.Empty;

  [JsonPropertyName("value")]
  public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Conflict specification defining mutually exclusive options.
/// </summary>
public sealed class ConflictSpec
{
  [JsonPropertyName("functions")]
  public List<string>? Functions { get; set; }

  [JsonPropertyName("behaviors")]
  public List<string>? Behaviors { get; set; }

  [JsonPropertyName("variants")]
  public List<string>? Variants { get; set; }

  [JsonPropertyName("features")]
  public List<string>? Features { get; set; }
}

/// <summary>
/// Filter predicate for filter tests.
/// </summary>
public sealed class FilterPredicate
{
  [JsonPropertyName("field")]
  public string Field { get; set; } = string.Empty;

  [JsonPropertyName("op")]
  public string Op { get; set; } = string.Empty;

  [JsonPropertyName("value")]
  public string Value { get; set; } = string.Empty;
}
