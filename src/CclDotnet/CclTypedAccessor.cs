using CatConfLang.TestRunner.Abstractions;

namespace CclDotnet;

/// <summary>
/// Typed accessor implementation for CCL hierarchies.
/// Supports path navigation using segment arrays and type conversions.
/// Uses boolean_lenient: accepts true/false/yes/no/on/off/1/0 (case-insensitive).
/// Uses list_coercion_enabled: single values and empty-key dicts are coerced to lists.
/// </summary>
public class CclTypedAccessor : ICclTypedAccess
{
  public string GetString(object hierarchy, params string[] pathSegments)
  {
    var value = NavigatePath(hierarchy, pathSegments);
    if (value is string s)
      return s;
    throw new CclParseException($"Value at path is not a string.");
  }

  public int GetInt(object hierarchy, params string[] pathSegments)
  {
    var value = NavigatePath(hierarchy, pathSegments);
    if (value is string s && int.TryParse(s, out var result))
      return result;
    throw new CclParseException($"Value at path is not a valid integer.");
  }

  public bool GetBool(object hierarchy, params string[] pathSegments)
  {
    var value = NavigatePath(hierarchy, pathSegments);
    if (value is string s)
    {
      return s.ToLowerInvariant() switch
      {
        "true" or "yes" or "on" or "1" => true,
        "false" or "no" or "off" or "0" => false,
        _ => throw new CclParseException($"Value '{s}' is not a valid boolean.")
      };
    }
    throw new CclParseException($"Value at path is not a string (cannot convert to bool).");
  }

  public double GetFloat(object hierarchy, params string[] pathSegments)
  {
    var value = NavigatePath(hierarchy, pathSegments);
    if (value is string s && double.TryParse(s, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var result))
      return result;
    throw new CclParseException($"Value at path is not a valid float.");
  }

  public IReadOnlyList<object> GetList(object hierarchy, params string[] pathSegments)
  {
    var value = NavigatePath(hierarchy, pathSegments);

    // Direct list
    if (value is List<object> list)
      return list;

    // Dict with empty key containing a list (bare list structure)
    if (value is Dictionary<string, object> dict &&
        dict.TryGetValue("", out var inner) && inner is List<object> bareList)
      return bareList;

    // list_coercion_enabled: single scalar value → single-item list
    if (value is string s)
      return new List<object> { s };

    throw new CclParseException($"Value at path is not a list.");
  }

  private static object NavigatePath(object current, string[] segments)
  {
    if (segments.Length == 0)
      throw new CclParseException("Path must have at least one segment.");

    foreach (var segment in segments)
    {
      if (current is Dictionary<string, object> dict)
      {
        if (!dict.TryGetValue(segment, out var next))
          throw new CclParseException($"Key '{segment}' not found.");
        current = next;
      }
      else
      {
        throw new CclParseException($"Cannot navigate through non-object at segment '{segment}'.");
      }
    }
    return current;
  }
}
