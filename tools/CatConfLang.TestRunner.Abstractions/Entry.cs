namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Represents a single key-value entry from parsing CCL text.
/// </summary>
public sealed record Entry(string Key, string Value);
