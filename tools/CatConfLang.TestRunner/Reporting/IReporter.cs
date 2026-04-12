using CatConfLang.TestRunner.Execution;
using CatConfLang.TestRunner.TestData;

namespace CatConfLang.TestRunner.Reporting;

public interface IReporter
{
  void RunStarted(RunContext context);
  void TestCompleted(TestOutcome outcome);
  void RunCompleted(RunSummary summary);
}

public sealed record RunContext(
    string ImplName,
    string ImplVersion,
    string TestDataPath,
    string ConfigPath,
    int TotalTests,
    bool Verbose,
    IReadOnlyCollection<string> ImplBehaviors,
    IReadOnlyCollection<string> ImplVariants);

public sealed record RunSummary(
    IReadOnlyList<TestOutcome> Outcomes,
    TimeSpan Duration)
{
  public int Passed => Outcomes.Count(o => o.Kind == OutcomeKind.Passed);
  public int Failed => Outcomes.Count(o => o.Kind == OutcomeKind.Failed);
  public int Skipped => Outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
  public int Todo => Outcomes.Count(o => o.Kind == OutcomeKind.Todo);
  public int Total => Outcomes.Count;
  public bool HasFailures => Failed > 0;
}
