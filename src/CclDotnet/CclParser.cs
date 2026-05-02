using System.Text;
using CatConfLang.TestRunner.Abstractions;
using CclDotnet.Pacman;

namespace CclDotnet;

/// <summary>
/// C# CCL implementation that delegates core parsing/model construction to the
/// F# Pacman parser and implements higher-level projections locally.
/// </summary>
public class CclParser : ICclParser
{
  private readonly ICclParser _core = new CclPacmanParser();

  public IReadOnlyList<Entry> Parse(string input)
  {
    return _core.Parse(input);
  }

  public IReadOnlyList<Entry> ParseIndented(string input)
  {
    return _core.ParseIndented(input);
  }

  public object BuildModel(string input)
  {
    return _core.BuildModel(input);
  }

  public object BuildHierarchy(string input)
  {
    var hierarchy = ProjectDictionary(AsModel(BuildModel(input)));
    PreserveDuplicateScalars(hierarchy, Parse(input));
    return hierarchy;
  }

  public object Load(string input)
  {
    return BuildHierarchy(input);
  }

  public string Print(string input)
  {
    return PrintEntries(Parse(input));
  }

  public string CanonicalFormat(string input)
  {
    return _core.CanonicalFormat(input);
  }

  private static IDictionary<string, object> AsModel(object value)
  {
    return value as IDictionary<string, object>
        ?? throw new CclParseException($"Expected CCL model dictionary, got {value.GetType().Name}.");
  }

  private static Dictionary<string, object> AsObjectDictionary(object value)
  {
    return value as Dictionary<string, object>
        ?? throw new CclParseException($"Expected hierarchy dictionary, got {value.GetType().Name}.");
  }

  private static Dictionary<string, object> ProjectDictionary(IDictionary<string, object> model)
  {
    var projected = new Dictionary<string, object>(StringComparer.Ordinal);
    foreach (var (key, value) in model)
      projected[key] = ProjectNode(AsModel(value));
    return projected;
  }

  private static object ProjectNode(IDictionary<string, object> model)
  {
    if (model.Count == 0)
      return "";

    if (model.Count == 1 && model.TryGetValue("", out var listModel))
    {
      var inner = AsModel(listModel);
      return inner.Count == 0
          ? ""
          : new Dictionary<string, object>(StringComparer.Ordinal) { [""] = ProjectList(inner) };
    }

    if (model.Values.All(IsEmptyModel))
      return model.Count == 1
          ? model.Keys.First()
          : model.Keys.Select(key => (object)key).ToList();

    return ProjectDictionary(model);
  }

  private static List<object> ProjectList(IDictionary<string, object> model)
  {
    var items = new List<object>();
    var objectGroup = new Dictionary<string, object>(StringComparer.Ordinal);

    void FlushObjectGroup()
    {
      if (objectGroup.Count == 0)
        return;

      items.AddRange(ExplodeDictionary(objectGroup));
      objectGroup.Clear();
    }

    foreach (var (key, value) in model)
    {
      if (IsEmptyModel(value))
      {
        FlushObjectGroup();
        items.Add(key);
      }
      else
      {
        objectGroup[key] = ProjectNode(AsModel(value));
      }
    }

    FlushObjectGroup();
    return items;
  }

  private static List<object> ExplodeValue(object value)
  {
    return value switch
    {
      Dictionary<string, object> dictionary => ExplodeDictionary(dictionary).Cast<object>().ToList(),
      List<object> list => list,
      _ => [value]
    };
  }

  private static List<Dictionary<string, object>> ExplodeDictionary(Dictionary<string, object> dictionary)
  {
    var values = dictionary.ToDictionary(pair => pair.Key, pair => ExplodeValue(pair.Value), StringComparer.Ordinal);
    var count = values.Values.Select(list => list.Count).DefaultIfEmpty(1).Max();
    var results = new List<Dictionary<string, object>>();

    for (var i = 0; i < count; i++)
    {
      var item = new Dictionary<string, object>(StringComparer.Ordinal);
      foreach (var (key, candidates) in values)
        item[key] = candidates[candidates.Count == 1 ? 0 : i];
      results.Add(item);
    }

    return results;
  }

  private static bool IsEmptyModel(object value)
  {
    return AsModel(value).Count == 0;
  }

  private static bool LooksNested(string value)
  {
    var firstNewline = value.IndexOf('\n');
    return firstNewline >= 0
        && value.Contains('=', StringComparison.Ordinal)
        && value[..firstNewline].Trim(' ', '\t', '\n', '\r').Length == 0;
  }

  private void PreserveDuplicateScalars(Dictionary<string, object> hierarchy, IReadOnlyList<Entry> entries)
  {
    foreach (var group in entries.GroupBy(entry => entry.Key))
    {
      var scalarCounts = group
          .Where(entry => !LooksNested(entry.Value))
          .GroupBy(entry => entry.Value)
          .ToDictionary(values => values.Key, values => values.Count(), StringComparer.Ordinal);

      if (scalarCounts.Count > 0 && hierarchy.TryGetValue(group.Key, out var projected))
        hierarchy[group.Key] = RestoreScalarDuplicates(projected, scalarCounts);

      var nestedEntries = group.Where(entry => LooksNested(entry.Value)).SelectMany(entry => ParseIndented(entry.Value)).ToList();
      if (nestedEntries.Count > 0 && hierarchy.TryGetValue(group.Key, out var nested) && nested is Dictionary<string, object> nestedDict)
        PreserveDuplicateScalars(nestedDict, nestedEntries);
    }
  }

  private static object RestoreScalarDuplicates(object projected, Dictionary<string, int> scalarCounts)
  {
    if (projected is string scalarValue && scalarCounts.TryGetValue(scalarValue, out var scalarCount) && scalarCount > 1)
      return Enumerable.Repeat<object>(scalarValue, scalarCount).ToList();

    if (projected is not List<object> list)
      return projected;

    foreach (var (value, expectedCount) in scalarCounts)
    {
      var actualCount = list.Count(item => item is string itemValue && itemValue == value);
      for (var i = actualCount; i < expectedCount; i++)
        list.Add(value);
    }

    return list;
  }

  private static string PrintEntries(IReadOnlyList<Entry> entries)
  {
    var builder = new StringBuilder();
    for (var i = 0; i < entries.Count; i++)
    {
      if (i > 0)
        builder.Append('\n');

      var entry = entries[i];
      builder.Append(string.IsNullOrEmpty(entry.Key) ? "=" : $"{entry.Key} =");
      if (!string.IsNullOrEmpty(entry.Value))
        builder.Append(entry.Value.StartsWith('\n') ? entry.Value : $" {entry.Value}");
    }
    return builder.ToString();
  }

  private static string FormatDictionary(Dictionary<string, object> dictionary, int depth)
  {
    var builder = new StringBuilder();
    foreach (var key in SortedKeys(dictionary))
      FormatValue(builder, depth, key, dictionary[key]);
    return builder.ToString();
  }

  private static string FormatModel(IDictionary<string, object> model, int depth)
  {
    var builder = new StringBuilder();
    foreach (var key in model.Keys.OrderBy(key => key == "" ? 0 : 1).ThenBy(key => key, StringComparer.Ordinal))
    {
      builder.Append(new string(' ', depth * 2));
      builder.Append(string.IsNullOrEmpty(key) ? "=" : $"{key} =");
      builder.Append('\n');

      var child = AsModel(model[key]);
      if (child.Count > 0)
        builder.Append(FormatModel(child, depth + 1));
    }
    return builder.ToString();
  }

  private static void FormatValue(StringBuilder builder, int depth, string key, object value)
  {
    var indent = new string(' ', depth * 2);
    switch (value)
    {
      case Dictionary<string, object> nested when key == "":
        builder.Append(indent).Append("=\n").Append(FormatDictionary(nested, depth + 1));
        break;
      case Dictionary<string, object> nested:
        builder.Append(indent).Append(key).Append(" =\n").Append(FormatDictionary(nested, depth + 1));
        break;
      case List<object> items:
        foreach (var item in items)
          FormatValue(builder, depth, key, item);
        break;
      default:
        builder.Append(indent);
        if (!string.IsNullOrEmpty(key))
          builder.Append(key).Append(" = ");
        else
          builder.Append("= ");
        builder.Append(value).Append('\n');
        break;
    }
  }

  private static IEnumerable<string> SortedKeys(Dictionary<string, object> dictionary)
  {
    return dictionary.Keys
        .OrderBy(key => key == "" ? 0 : 1)
        .ThenBy(key => key, StringComparer.Ordinal);
  }
}
