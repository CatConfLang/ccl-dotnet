namespace CatConfLang.TestRunner.TestData;

/// <summary>
/// Configuration declaring what a CCL implementation supports.
/// Used to filter the test suite to only compatible tests.
/// </summary>
public sealed class ImplementationConfig
{
  public HashSet<string> Functions { get; init; } = [];
  public HashSet<string> Features { get; init; } = [];
  public HashSet<string> Behaviors { get; init; } = [];
  public HashSet<string> Variants { get; init; } = [];

  public bool HasFunction(string fn) => Functions.Contains(fn);
  public bool HasFeature(string feature) => Features.Contains(feature);
  public bool HasBehavior(string behavior) => Behaviors.Contains(behavior);
  public bool HasVariant(string variant) => Variants.Contains(variant);
}
