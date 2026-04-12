namespace CatConfLang.TestRunner.Execution;

/// <summary>
/// Signals that the current test case should be reported as skipped.
/// Replaces xUnit.SkippableFact's <c>Skip.If</c>.
/// </summary>
public sealed class SkipException(string reason) : Exception(reason);

/// <summary>
/// Signals an assertion failure inside the test executor. Carries structured
/// expected/actual strings for the reporter.
/// </summary>
public sealed class TestAssertionException(string message, string? expected = null, string? actual = null)
    : Exception(message)
{
  public string? Expected { get; } = expected;
  public string? Actual { get; } = actual;
}
