namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Core CCL parsing interface. Implementations must provide parse, and may opt into
/// build_hierarchy (JSON-friendly nested objects with scalar leaves) and/or build_model
/// (the canonical recursive Map&lt;string, Model&gt; with no scalar leaves and merging
/// duplicate keys — see https://catconflang.com/reference/functions/#build_model).
/// </summary>
public interface ICclParser
{
  IReadOnlyList<Entry> Parse(string input);

  IReadOnlyList<Entry> ParseIndented(string input);

  /// <summary>
  /// Builds a JSON-friendly nested-object projection with actual scalar leaves.
  /// Distinct from <see cref="BuildModel"/>; the two are independent peers.
  /// </summary>
  object BuildHierarchy(string input);

  /// <summary>
  /// Builds the canonical CCL data model: a recursive Map&lt;string, Model&gt; where
  /// values become keys pointing to inner maps (no scalar leaves) and duplicate keys
  /// merge their inner maps recursively. Conventionally returned as
  /// <c>IDictionary&lt;string, object&gt;</c> whose leaf values are themselves
  /// <c>IDictionary&lt;string, object&gt;</c> (often empty).
  /// </summary>
  object BuildModel(string input);

  object Load(string input);

  string Print(string input);

  string CanonicalFormat(string input);
}
