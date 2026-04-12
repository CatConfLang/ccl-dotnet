namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Processing functions for filtering and composing CCL entries.
/// </summary>
public interface ICclProcessing
{
  IReadOnlyList<Entry> Filter(IReadOnlyList<Entry> entries, string field, string op, string value);

  IReadOnlyList<Entry> Compose(params string[] inputs);
}
