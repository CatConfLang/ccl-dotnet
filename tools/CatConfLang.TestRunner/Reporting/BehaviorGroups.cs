namespace CatConfLang.TestRunner.Reporting;

/// <summary>
/// Mutually-exclusive behavior groups defined by the CCL spec. Lets reporters
/// render related behaviors next to each other so the reader can see, at a
/// glance, which side of each pair the implementation chose and which side
/// produced the corresponding skips.
///
/// See https://catconflang.com/test-suite-guide/ for the canonical group list.
/// Behaviors not in this map are reported under the "other" group.
/// </summary>
public static class BehaviorGroups
{
  public const string Other = "other";

  private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
  {
    ["toplevel_indent_strip"] = "continuation",
    ["toplevel_indent_preserve"] = "continuation",

    ["crlf_preserve_literal"] = "line_endings",
    ["crlf_normalize_to_lf"] = "line_endings",

    ["boolean_lenient"] = "booleans",
    ["boolean_strict"] = "booleans",

    ["continuation_tab_to_space"] = "tabs",
    ["continuation_tab_preserve"] = "tabs",
    ["tabs_as_content"] = "tabs",

    ["indent_spaces"] = "indentation",
    ["indent_tabs"] = "indentation",

    ["list_coercion_enabled"] = "list_coercion",
    ["list_coercion_disabled"] = "list_coercion",

    ["array_order_insertion"] = "array_order",
    ["array_order_lexicographic"] = "array_order",

    ["delimiter_first_equals"] = "delimiter",
    ["delimiter_prefer_spaced"] = "delimiter",
  };

  public static string GroupOf(string behavior) =>
      Map.TryGetValue(behavior, out var g) ? g : Other;
}
