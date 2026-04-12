using CatConfLang.TestRunner.TestData;

namespace CatConfLang.TestRunner.Execution;

public enum OutcomeKind { Passed, Failed, Skipped, Todo }

/// <summary>
/// Result of executing one test case. <see cref="Failed"/> outcomes carry rendered
/// expected/actual strings so reporters can show a diff without having to know
/// anything about CCL semantics.
/// </summary>
public sealed record TestOutcome(
    CclTestCase Test,
    OutcomeKind Kind,
    string? Reason = null,
    string? Expected = null,
    string? Actual = null,
    Exception? Exception = null,
    TimeSpan Duration = default)
{
  public static TestOutcome Pass(CclTestCase test, TimeSpan duration) =>
      new(test, OutcomeKind.Passed, Duration: duration);

  public static TestOutcome Fail(CclTestCase test, string reason, string? expected, string? actual, Exception? ex, TimeSpan duration) =>
      new(test, OutcomeKind.Failed, reason, expected, actual, ex, duration);

  public static TestOutcome Skip(CclTestCase test, string reason) =>
      new(test, OutcomeKind.Skipped, reason);

  public static TestOutcome Todo(CclTestCase test, string reason) =>
      new(test, OutcomeKind.Todo, reason);
}
