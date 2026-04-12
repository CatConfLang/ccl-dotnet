using System.Text;
using CatConfLang.TestRunner.Abstractions;

namespace CclDotnet;

/// <summary>
/// CCL parser implementation using toplevel_indent_preserve behavior.
/// A single parsing algorithm is used for both top-level and nested contexts:
/// baseline indentation N is always determined from the first non-empty line.
/// </summary>
public class CclParser : ICclParser
{
  public IReadOnlyList<Entry> Parse(string input)
  {
    return ParseInternal(input, allowKeyOnly: false);
  }

  public IReadOnlyList<Entry> ParseIndented(string input)
  {
    return ParseInternal(input, allowKeyOnly: true);
  }

  public object BuildHierarchy(string input)
  {
    var entries = ParseInternal(input, allowKeyOnly: true);
    return BuildHierarchyFromEntries(entries);
  }

  public object Load(string input)
  {
    return BuildHierarchy(input);
  }

  public string Print(string input)
  {
    var entries = ParseInternal(input, allowKeyOnly: true);
    return PrintEntries(entries);
  }

  public string CanonicalFormat(string input)
  {
    var hierarchy = BuildHierarchy(input);
    return FormatCanonical(hierarchy, 0);
  }

  /// <summary>
  /// Core parsing: converts CCL text into a flat list of key-value entries.
  /// Uses toplevel_indent_preserve, tabs_as_content, delimiter_first_equals,
  /// and crlf_preserve_literal behaviors.
  /// </summary>
  internal static List<Entry> ParseInternal(string input, bool allowKeyOnly = true)
  {
    if (string.IsNullOrEmpty(input))
      return [];

    var lines = SplitLines(input);
    var baseline = DetermineBaseline(lines);
    var entries = new List<Entry>();
    var i = 0;

    while (i < lines.Count)
    {
      if (IsEmptyLine(lines[i]))
      {
        i++;
        continue;
      }

      var lineIndent = CountLeadingSpaces(lines[i]);

      // Only process lines at or below baseline as potential entry starts
      if (lineIndent > baseline)
      {
        i++;
        continue;
      }

      var eqIdx = lines[i].IndexOf('=');
      if (eqIdx >= 0)
      {
        // Normal entry: split on first '='
        var rawKey = lines[i][..eqIdx];
        var key = TrimAllWhitespace(rawKey);
        var afterEq = lines[i][(eqIdx + 1)..];
        var firstLineValue = TrimLeadingSpaces(afterEq);

        var valueBuilder = new StringBuilder();
        valueBuilder.Append(firstLineValue);
        i++;
        CollectContinuations(lines, ref i, baseline, valueBuilder);

        var value = TrimTrailingSpaces(valueBuilder.ToString());
        entries.Add(new Entry(key, value));
      }
      else
      {
        // Line without '=' — check if next baseline line starts with '='
        // (indicating a multi-line key like "key \n= value")
        var pendingKeyLine = lines[i];
        i++;

        // Skip empty lines to find the next meaningful line
        var j = i;
        while (j < lines.Count && IsEmptyLine(lines[j]))
          j++;

        if (j < lines.Count && CountLeadingSpaces(lines[j]) <= baseline)
        {
          var nextEq = lines[j].IndexOf('=');
          if (nextEq >= 0)
          {
            // Check if '=' is the first non-space char on next line
            var beforeEq = lines[j][..nextEq];
            if (IsEmptyLine(beforeEq))
            {
              // Multi-line key: "key \n= value"
              var key = TrimAllWhitespace(pendingKeyLine);
              var afterEq = lines[j][(nextEq + 1)..];
              var firstLineValue = TrimLeadingSpaces(afterEq);

              var valueBuilder = new StringBuilder();
              valueBuilder.Append(firstLineValue);
              i = j + 1;
              CollectContinuations(lines, ref i, baseline, valueBuilder);

              var value = TrimTrailingSpaces(valueBuilder.ToString());
              entries.Add(new Entry(key, value));
              continue;
            }
          }
        }

        if (!allowKeyOnly)
        {
          // In parse() mode, skip lines without '='
          continue;
        }

        // Key-only entry (no '=' found on this line or as continuation)
        var keyOnly = TrimAllWhitespace(pendingKeyLine);

        // Collect continuation lines as value
        var valBuilder = new StringBuilder();
        CollectContinuations(lines, ref i, baseline, valBuilder);
        var val = TrimTrailingSpaces(valBuilder.ToString());

        entries.Add(new Entry(keyOnly, val));
      }
    }

    return entries;
  }

  /// <summary>
  /// Collects continuation lines (indent > baseline) into the value builder.
  /// Empty lines are included if more continuations follow.
  /// </summary>
  private static void CollectContinuations(List<string> lines, ref int i, int baseline, StringBuilder valueBuilder)
  {
    while (i < lines.Count)
    {
      if (IsEmptyLine(lines[i]))
      {
        if (HasMoreContinuations(lines, i + 1, baseline))
        {
          valueBuilder.Append('\n');
          valueBuilder.Append(lines[i]);
          i++;
          continue;
        }
        break;
      }

      var indent = CountLeadingSpaces(lines[i]);
      if (indent > baseline)
      {
        valueBuilder.Append('\n');
        valueBuilder.Append(lines[i]);
        i++;
      }
      else
      {
        break;
      }
    }
  }

  /// <summary>
  /// Builds a hierarchical structure from flat entries via recursive fixed-point parsing.
  /// </summary>
  internal static object BuildHierarchyFromEntries(IReadOnlyList<Entry> entries)
  {
    var result = new Dictionary<string, object>();

    foreach (var entry in entries)
    {
      if (entry.Key == "")
      {
        // Empty key = list item. Recurse the value if it's structured.
        var val = MaybeRecurse(entry.Value);
        AddToList(result, "", val);
      }
      else if (ContainsCclSyntax(entry.Value))
      {
        var nestedEntries = ParseInternal(entry.Value);
        var nested = BuildHierarchyFromEntries(nestedEntries);
        MergeIntoDict(result, entry.Key, nested);
      }
      else
      {
        AddToDict(result, entry.Key, entry.Value);
      }
    }

    return result;
  }

  /// <summary>
  /// Recursively parses a value if it's a structured multiline value.
  /// </summary>
  private static object MaybeRecurse(string value)
  {
    if (ContainsCclSyntax(value))
    {
      var nestedEntries = ParseInternal(value);
      return BuildHierarchyFromEntries(nestedEntries);
    }
    return value;
  }

  /// <summary>
  /// Adds a value to a dict key, converting to list on duplicate keys.
  /// </summary>
  private static void AddToDict(Dictionary<string, object> dict, string key, object value)
  {
    if (dict.TryGetValue(key, out var existing))
    {
      if (existing is List<object> list)
        list.Add(value);
      else
        dict[key] = new List<object> { existing, value };
    }
    else
    {
      dict[key] = value;
    }
  }

  /// <summary>
  /// Merges a nested dict value into an existing key. If both old and new values are
  /// dicts, merge their contents. Otherwise, falls back to AddToDict behavior.
  /// </summary>
  private static void MergeIntoDict(Dictionary<string, object> dict, string key, object value)
  {
    if (dict.TryGetValue(key, out var existing))
    {
      if (existing is Dictionary<string, object> existingDict && value is Dictionary<string, object> newDict)
      {
        // Merge dicts: add all new keys to existing
        foreach (var kvp in newDict)
        {
          if (existingDict.ContainsKey(kvp.Key))
            AddToDict(existingDict, kvp.Key, kvp.Value);
          else
            existingDict[kvp.Key] = kvp.Value;
        }
      }
      else
      {
        // Can't merge, convert to list
        if (existing is List<object> list)
          list.Add(value);
        else
          dict[key] = new List<object> { existing, value };
      }
    }
    else
    {
      dict[key] = value;
    }
  }

  private static void AddToList(Dictionary<string, object> dict, string key, object value)
  {
    if (dict.TryGetValue(key, out var existing))
    {
      if (existing is List<object> list)
        list.Add(value);
      else
        dict[key] = new List<object> { existing, value };
    }
    else
    {
      dict[key] = new List<object> { value };
    }
  }

  /// <summary>
  /// Checks if a value is a structured multiline that should be recursively parsed.
  /// Returns true when the first line of the value is empty (only spaces/\r),
  /// indicating nested CCL content on subsequent lines.
  /// </summary>
  private static bool ContainsCclSyntax(string value)
  {
    var firstNewline = value.IndexOf('\n');
    if (firstNewline < 0)
      return false; // Single-line → terminal value

    // First line (before \n) must be empty (only spaces and \r)
    for (var idx = 0; idx < firstNewline; idx++)
    {
      if (value[idx] != ' ' && value[idx] != '\r')
        return false; // Has content on first line
    }

    return true;
  }

  private static List<string> SplitLines(string text)
  {
    var lines = new List<string>();
    var start = 0;
    for (var j = 0; j < text.Length; j++)
    {
      if (text[j] == '\n')
      {
        lines.Add(text[start..j]);
        start = j + 1;
      }
    }
    if (start <= text.Length)
      lines.Add(text[start..]);
    return lines;
  }

  /// <summary>
  /// Determines baseline indentation from the first non-empty line.
  /// </summary>
  private static int DetermineBaseline(List<string> lines)
  {
    foreach (var line in lines)
    {
      if (!IsEmptyLine(line))
        return CountLeadingSpaces(line);
    }
    return 0;
  }

  /// <summary>
  /// Counts leading space characters only (tabs_as_content).
  /// </summary>
  private static int CountLeadingSpaces(string line)
  {
    var count = 0;
    foreach (var c in line)
    {
      if (c == ' ') count++;
      else break;
    }
    return count;
  }

  /// <summary>
  /// A line is empty if it contains only spaces and \r characters.
  /// With tabs_as_content, tabs are content (not whitespace).
  /// \r is treated as non-meaningful for structure (crlf_preserve_literal).
  /// </summary>
  private static bool IsEmptyLine(string line)
  {
    foreach (var c in line)
    {
      if (c != ' ' && c != '\r')
        return false;
    }
    return true;
  }

  /// <summary>
  /// Trims ALL whitespace (spaces, tabs, newlines, CRs) from a string.
  /// </summary>
  private static string TrimAllWhitespace(string s)
  {
    // Trim leading and trailing whitespace (spaces, tabs, newlines, carriage returns)
    var start = 0;
    while (start < s.Length && (s[start] == ' ' || s[start] == '\t' || s[start] == '\n' || s[start] == '\r'))
      start++;
    var end = s.Length - 1;
    while (end >= start && (s[end] == ' ' || s[end] == '\t' || s[end] == '\n' || s[end] == '\r'))
      end--;
    return start > end ? "" : s[start..(end + 1)];
  }

  /// <summary>
  /// Trims leading spaces only (tabs are content, not whitespace).
  /// </summary>
  private static string TrimLeadingSpaces(string s)
  {
    var idx = 0;
    while (idx < s.Length && s[idx] == ' ')
      idx++;
    return s[idx..];
  }

  /// <summary>
  /// Trims trailing spaces only.
  /// </summary>
  private static string TrimTrailingSpaces(string s)
  {
    var end = s.Length;
    while (end > 0 && s[end - 1] == ' ')
      end--;
    return s[..end];
  }

  /// <summary>
  /// Checks if more continuation lines (indent > baseline) exist from startIndex onward.
  /// </summary>
  private static bool HasMoreContinuations(List<string> lines, int startIndex, int baseline)
  {
    for (var j = startIndex; j < lines.Count; j++)
    {
      if (IsEmptyLine(lines[j]))
        continue;
      return CountLeadingSpaces(lines[j]) > baseline;
    }
    return false;
  }

  /// <summary>
  /// Converts entries back to CCL text. Structure-preserving:
  /// for standard-format input, print(parse(x)) == x.
  /// </summary>
  internal static string PrintEntries(IReadOnlyList<Entry> entries)
  {
    var sb = new StringBuilder();
    for (var idx = 0; idx < entries.Count; idx++)
    {
      if (idx > 0)
        sb.Append('\n');

      var entry = entries[idx];
      if (string.IsNullOrEmpty(entry.Key))
      {
        sb.Append("= ");
        sb.Append(entry.Value);
      }
      else if (string.IsNullOrEmpty(entry.Value))
      {
        sb.Append(entry.Key);
        sb.Append(" =");
      }
      else if (entry.Value.StartsWith('\n') || entry.Value.StartsWith("\r\n"))
      {
        // Multi-line value where first line after = is empty
        sb.Append(entry.Key);
        sb.Append(" =");
        sb.Append(entry.Value);
      }
      else
      {
        sb.Append(entry.Key);
        sb.Append(" = ");
        sb.Append(entry.Value);
      }
    }
    return sb.ToString();
  }

  private static string FormatCanonical(object value, int depth)
  {
    var indent = new string(' ', depth * 2);
    var sb = new StringBuilder();

    if (value is Dictionary<string, object> dict)
    {
      // Sort keys: empty key ("") first for bare lists, then alphabetical
      var sortedKeys = dict.Keys
          .OrderBy(k => k == "" ? 0 : 1)
          .ThenBy(k => k, StringComparer.Ordinal)
          .ToList();

      foreach (var key in sortedKeys)
      {
        var val = dict[key];

        if (key == "" && val is List<object> bareItems)
        {
          for (var i = 0; i < bareItems.Count; i++)
          {
            var item = bareItems[i];
            if (item is Dictionary<string, object>)
            {
              sb.Append(indent);
              sb.Append("=\n");
              sb.Append(FormatCanonical(item, depth + 1));
            }
            else
            {
              sb.Append(indent);
              sb.Append("= ");
              sb.Append(item);
              sb.Append('\n');
            }
          }
        }
        else if (val is Dictionary<string, object>)
        {
          sb.Append(indent);
          sb.Append(key);
          sb.Append(" =\n");
          sb.Append(FormatCanonical(val, depth + 1));
        }
        else if (val is List<object> list)
        {
          foreach (var item in list)
          {
            if (item is Dictionary<string, object>)
            {
              sb.Append(indent);
              sb.Append(key);
              sb.Append(" =\n");
              sb.Append(FormatCanonical(item, depth + 1));
            }
            else
            {
              sb.Append(indent);
              sb.Append(key);
              sb.Append(" =\n");
              sb.Append(indent);
              sb.Append("  ");
              var trimmed = item?.ToString()?.Trim(' ', '\t') ?? "";
              sb.Append(trimmed);
              sb.Append(" =\n");
            }
          }
        }
        else if (val is string s)
        {
          sb.Append(indent);
          sb.Append(key);
          sb.Append(" =\n");
          if (s != "")
          {
            var trimmed = s.Trim(' ', '\t');
            sb.Append(indent);
            sb.Append("  ");
            sb.Append(trimmed);
            sb.Append(" =\n");
          }
        }
      }
    }
    else if (value is string s)
    {
      var trimmed = s.Trim(' ', '\t');
      sb.Append(indent);
      sb.Append(trimmed);
      sb.Append(" =\n");
    }

    return sb.ToString();
  }
}
