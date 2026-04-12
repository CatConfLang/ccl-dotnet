namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Core CCL parsing interface. Implementations must provide parse and build_hierarchy.
/// </summary>
public interface ICclParser
{
  IReadOnlyList<Entry> Parse(string input);

  IReadOnlyList<Entry> ParseIndented(string input);

  object BuildHierarchy(string input);

  object Load(string input);

  string Print(string input);

  string CanonicalFormat(string input);
}
