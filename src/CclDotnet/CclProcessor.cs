using CatConfLang.TestRunner.Abstractions;
using System.Text.RegularExpressions;

namespace CclDotnet;

/// <summary>
/// Processing functions for CCL entries: filter and compose.
/// </summary>
public class CclProcessor : ICclProcessing
{
  public IReadOnlyList<Entry> Filter(IReadOnlyList<Entry> entries, string field, string op, string value)
  {
    return entries.Where(e => EvaluatePredicate(e, field, op, value)).ToList();
  }

  public IReadOnlyList<Entry> Compose(params string[] inputs)
  {
    var parser = new CclParser();
    var result = new List<Entry>();
    foreach (var input in inputs)
    {
      if (!string.IsNullOrEmpty(input))
      {
        result.AddRange(parser.Parse(NormalizeComposedEmptyKeys(input)));
      }
    }
    return result;
  }

  private static bool EvaluatePredicate(Entry entry, string field, string op, string value)
  {
    var fieldValue = field switch
    {
      "key" => entry.Key,
      "value" => entry.Value,
      _ => throw new CclParseException($"Unknown filter field: {field}")
    };

    return op switch
    {
      "==" or "eq" => fieldValue == value,
      "!=" or "neq" => fieldValue != value,
      "starts_with" => fieldValue.StartsWith(value, StringComparison.Ordinal),
      "ends_with" => fieldValue.EndsWith(value, StringComparison.Ordinal),
      "contains" => fieldValue.Contains(value, StringComparison.Ordinal),
      _ => throw new CclParseException($"Unknown filter operator: {op}")
    };
  }

  private static string NormalizeComposedEmptyKeys(string input)
  {
    return Regex.Replace(input, "(?m)^[ \t]+=", "=");
  }
}
