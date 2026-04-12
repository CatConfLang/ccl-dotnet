using CatConfLang.TestRunner.Execution;

namespace CatConfLang.TestRunner.Reporting;

/// <summary>
/// ANSI-free reporter for non-TTY / CI environments. Emits one line per failure
/// (always) and per test (when verbose), plus end-of-run aggregate counts.
/// </summary>
public sealed class PlainReporter(TextWriter writer, bool verbose) : IReporter
{
  private IReadOnlyCollection<string> implBehaviors = [];
  private IReadOnlyCollection<string> implVariants = [];

  public void RunStarted(RunContext ctx)
  {
    implBehaviors = ctx.ImplBehaviors;
    implVariants = ctx.ImplVariants;

    writer.WriteLine($"CCL test runner — {ctx.ImplName} {ctx.ImplVersion}");
    writer.WriteLine($"  test data: {ctx.TestDataPath}");
    writer.WriteLine($"  config:    {ctx.ConfigPath}");
    writer.WriteLine($"  tests:     {ctx.TotalTests} total");
    writer.WriteLine($"  behaviors: {(ctx.ImplBehaviors.Count == 0 ? "(none)" : string.Join(", ", ctx.ImplBehaviors.OrderBy(b => b, StringComparer.Ordinal)))}");
    writer.WriteLine($"  variants:  {(ctx.ImplVariants.Count == 0 ? "(none)" : string.Join(", ", ctx.ImplVariants.OrderBy(v => v, StringComparer.Ordinal)))}");
    writer.WriteLine();
  }

  public void TestCompleted(TestOutcome outcome)
  {
    switch (outcome.Kind)
    {
      case OutcomeKind.Passed:
        if (verbose)
          writer.WriteLine($"PASS  {outcome.Test.Validation,-22} {outcome.Test.Name}");
        break;

      case OutcomeKind.Skipped:
        if (verbose)
          writer.WriteLine($"SKIP  {outcome.Test.Validation,-22} {outcome.Test.Name} — {outcome.Reason}");
        break;

      case OutcomeKind.Todo:
        if (verbose)
          writer.WriteLine($"TODO  {outcome.Test.Validation,-22} {outcome.Test.Name} — {outcome.Reason}");
        break;

      case OutcomeKind.Failed:
        writer.WriteLine($"FAIL  {outcome.Test.Validation,-22} {outcome.Test.Name}");
        writer.WriteLine($"      reason:   {outcome.Reason}");
        if (outcome.Test.Inputs.Count > 0)
        {
          writer.WriteLine($"      input:    {Truncate(outcome.Test.Inputs[0])}");
        }
        if (outcome.Expected is not null)
          writer.WriteLine($"      expected: {Truncate(outcome.Expected)}");
        if (outcome.Actual is not null)
          writer.WriteLine($"      actual:   {Truncate(outcome.Actual)}");
        break;
    }
  }

  public void RunCompleted(RunSummary summary)
  {
    writer.WriteLine();
    writer.WriteLine("Validation         Pass  Fail  Skip  Todo  Total  Why skipped/todo");
    writer.WriteLine("------------------------------------------------------------------");
    var byValidation = summary.Outcomes
        .GroupBy(o => o.Test.Validation)
        .OrderBy(g => g.Key);
    foreach (var group in byValidation)
    {
      var outcomes = group.ToList();
      var pass = outcomes.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = outcomes.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = outcomes.Count(o => o.Kind == OutcomeKind.Todo);
      var total = outcomes.Count;
      writer.WriteLine($"{group.Key,-18} {pass,5} {fail,5} {skip,5} {todo,5} {total,6}  {FormatSkipReasons(outcomes)}");
    }

    WriteBehaviorBreakdown(summary);
    WriteVariantBreakdown(summary);

    writer.WriteLine();
    writer.WriteLine($"Totals: {summary.Passed} passed, {summary.Failed} failed, {summary.Skipped} skipped, {summary.Todo} todo, {summary.Total} total ({summary.Duration.TotalMilliseconds:F0} ms)");
  }

  private static string FormatSkipReasons(IEnumerable<TestOutcome> outcomes)
  {
    var byReason = outcomes
        .Where(o => o.Kind is OutcomeKind.Skipped or OutcomeKind.Todo && !string.IsNullOrEmpty(o.Reason))
        .GroupBy(o => o.Reason!)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key, StringComparer.Ordinal)
        .ToList();

    return byReason.Count == 0
        ? ""
        : string.Join("; ", byReason.Select(g => $"{g.Key} ({g.Count()})"));
  }

  private void WriteBehaviorBreakdown(RunSummary summary)
  {
    var grouped = summary.Outcomes
        .SelectMany(o => o.Test.Behaviors.Select(tag => (tag, o)))
        .GroupBy(x => x.tag)
        .Select(g => (Tag: g.Key, Group: BehaviorGroups.GroupOf(g.Key), Outcomes: g.ToList()))
        .OrderBy(x => x.Group == BehaviorGroups.Other ? 1 : 0)
        .ThenBy(x => x.Group, StringComparer.Ordinal)
        .ThenBy(x => x.Tag, StringComparer.Ordinal)
        .ToList();
    if (grouped.Count == 0)
      return;

    const string label = "Behavior (★ = impl-chosen)";
    var keyWidth = Math.Max(label.Length, grouped.Max(g => g.Tag.Length + 2));
    var groupWidth = Math.Max("Group".Length, grouped.Max(g => g.Group.Length));

    writer.WriteLine();
    writer.WriteLine($"{"Group".PadRight(groupWidth)}  {label.PadRight(keyWidth)}  Pass  Fail  Skip  Todo  Why skipped/todo");
    writer.WriteLine(new string('-', groupWidth + keyWidth + 46));

    string? lastGroup = null;
    foreach (var (tag, group, outcomes) in grouped)
    {
      var os = outcomes.Select(x => x.o).ToList();
      var pass = os.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = os.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = os.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = os.Count(o => o.Kind == OutcomeKind.Todo);

      var groupCell = group == lastGroup ? "" : group;
      lastGroup = group;
      var marker = implBehaviors.Contains(tag) ? "★ " : "  ";

      writer.WriteLine(
          $"{groupCell.PadRight(groupWidth)}  {(marker + tag).PadRight(keyWidth)}  {pass,5} {fail,5} {skip,5} {todo,5}  {FormatSkipReasons(os)}");
    }
  }

  private void WriteVariantBreakdown(RunSummary summary)
  {
    var grouped = summary.Outcomes
        .SelectMany(o => o.Test.Variants.Select(tag => (tag, o)))
        .GroupBy(x => x.tag)
        .OrderBy(g => g.Key, StringComparer.Ordinal)
        .ToList();
    if (grouped.Count == 0)
      return;

    const string label = "Variant (★ = impl-targeted)";
    var keyWidth = Math.Max(label.Length, grouped.Max(g => g.Key.Length + 2));

    writer.WriteLine();
    writer.WriteLine($"{label.PadRight(keyWidth)}  Pass  Fail  Skip  Todo  Why skipped/todo");
    writer.WriteLine(new string('-', keyWidth + 44));
    foreach (var group in grouped)
    {
      var os = group.Select(x => x.o).ToList();
      var pass = os.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = os.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = os.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = os.Count(o => o.Kind == OutcomeKind.Todo);
      var marker = implVariants.Contains(group.Key) ? "★ " : "  ";
      writer.WriteLine($"{(marker + group.Key).PadRight(keyWidth)}  {pass,5} {fail,5} {skip,5} {todo,5}  {FormatSkipReasons(os)}");
    }
  }

  private static string Truncate(string s, int max = 200)
  {
    s = s.Replace("\n", "\\n").Replace("\t", "\\t");
    return s.Length <= max ? s : s[..max] + "…";
  }
}
