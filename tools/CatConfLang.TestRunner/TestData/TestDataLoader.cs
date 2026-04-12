using System.Text.Json;

namespace CatConfLang.TestRunner.TestData;

/// <summary>
/// Loads and filters CCL test cases from the test suite JSON files.
/// </summary>
public static class TestDataLoader
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = false,
    AllowTrailingCommas = true,
  };

  public static List<CclTestCase> LoadAllTests(string testDataPath)
  {
    var tests = new List<CclTestCase>();
    var jsonFiles = Directory.GetFiles(testDataPath, "*.json");

    foreach (var file in jsonFiles)
    {
      var json = File.ReadAllText(file);
      var suite = JsonSerializer.Deserialize<TestSuiteFile>(json, JsonOptions);
      if (suite?.Tests is not null)
      {
        tests.AddRange(suite.Tests);
      }
    }

    return tests;
  }

  public static List<CclTestCase> LoadCompatibleTests(string testDataPath, ImplementationConfig config)
  {
    var allTests = LoadAllTests(testDataPath);
    return allTests.Where(t => IsTestCompatible(t, config)).ToList();
  }

  /// <summary>
  /// Classification of a test against an implementation's config. Tests that the
  /// implementation does not support are reported as <c>todo</c> (function not
  /// implemented) or <c>skip</c> (filtered by behavior/variant/feature conflict),
  /// per the CCL test-results-format spec.
  /// </summary>
  public enum CompatibilityKind { Run, Todo, Skip }

  public sealed record Compatibility(CompatibilityKind Kind, string? Reason);

  /// <summary>
  /// Classify a test against an implementation's config without dropping it.
  /// </summary>
  public static Compatibility Classify(CclTestCase test, ImplementationConfig config)
  {
    var unimplemented = test.Functions
        .Where(fn => !IsFunctionSupported(fn, config))
        .ToList();
    if (unimplemented.Count > 0)
      return new Compatibility(CompatibilityKind.Todo, $"unimplemented function(s): {string.Join(", ", unimplemented)}");

    foreach (var feature in test.Features)
    {
      if ((feature.StartsWith("experimental_", StringComparison.Ordinal) ||
           feature.StartsWith("optional_", StringComparison.Ordinal)) &&
          !config.HasFeature(feature))
      {
        return new Compatibility(CompatibilityKind.Skip, $"opt-in feature not enabled: {feature}");
      }
    }

    if (test.Variants.Count > 0 && !test.Variants.Any(config.HasVariant))
      return new Compatibility(CompatibilityKind.Skip, $"impl does not target variant(s): {string.Join(", ", test.Variants)}");

    if (test.Conflicts is not null)
    {
      var conflictingBehavior = test.Conflicts.Behaviors?.FirstOrDefault(config.HasBehavior);
      if (conflictingBehavior is not null)
        return new Compatibility(CompatibilityKind.Skip, $"behavior conflict: {conflictingBehavior}");

      var conflictingVariant = test.Conflicts.Variants?.FirstOrDefault(config.HasVariant);
      if (conflictingVariant is not null)
        return new Compatibility(CompatibilityKind.Skip, $"variant conflict: {conflictingVariant}");

      var conflictingFeature = test.Conflicts.Features?.FirstOrDefault(config.HasFeature);
      if (conflictingFeature is not null)
        return new Compatibility(CompatibilityKind.Skip, $"feature conflict: {conflictingFeature}");

      var conflictingFunction = test.Conflicts.Functions?.FirstOrDefault(config.HasFunction);
      if (conflictingFunction is not null)
        return new Compatibility(CompatibilityKind.Skip, $"function conflict: {conflictingFunction}");
    }

    return new Compatibility(CompatibilityKind.Run, null);
  }

  /// <summary>
  /// Map of composite functions to their actual required dependencies.
  /// Some test functions (like compose_associative) are not standalone —
  /// they are validations that require a combination of other functions.
  ///
  /// TODO: Remove this workaround once ccl-test-data is fixed to list actual
  /// required functions instead of composite function names.
  /// See: https://github.com/CatConfLang/ccl-test-data/issues/115
  /// </summary>
  private static readonly Dictionary<string, string[]> CompositeFunctions = new()
  {
    ["compose_associative"] = ["parse", "compose", "build_hierarchy"],
    ["identity_left"] = ["parse", "compose", "build_hierarchy"],
    ["identity_right"] = ["parse", "compose", "build_hierarchy"],
  };

  private static bool IsFunctionSupported(string function, ImplementationConfig config)
  {
    if (config.HasFunction(function))
      return true;

    if (CompositeFunctions.TryGetValue(function, out var deps))
      return deps.All(config.HasFunction);

    return false;
  }

  public static bool IsTestCompatible(CclTestCase test, ImplementationConfig config)
  {
    if (!test.Functions.All(fn => IsFunctionSupported(fn, config)))
      return false;

    foreach (var feature in test.Features)
    {
      if (feature.StartsWith("experimental_", StringComparison.Ordinal) ||
          feature.StartsWith("optional_", StringComparison.Ordinal))
      {
        if (!config.HasFeature(feature))
          return false;
      }
    }

    if (test.Variants.Count > 0)
    {
      if (!test.Variants.Any(config.HasVariant))
        return false;
    }

    if (test.Conflicts is not null)
    {
      if (test.Conflicts.Behaviors?.Any(config.HasBehavior) == true)
        return false;

      if (test.Conflicts.Variants?.Any(config.HasVariant) == true)
        return false;

      if (test.Conflicts.Features?.Any(config.HasFeature) == true)
        return false;

      if (test.Conflicts.Functions?.Any(config.HasFunction) == true)
        return false;
    }

    return true;
  }
}
