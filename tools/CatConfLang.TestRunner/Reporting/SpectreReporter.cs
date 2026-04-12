using System.Text;
using CatConfLang.TestRunner.Execution;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Spectre.Console;

namespace CatConfLang.TestRunner.Reporting;

/// <summary>
/// Rich reporter using Spectre.Console: header panel, per-failure panels with
/// expected/actual/input, and three end-of-run summary tables (validation,
/// function tags, feature tags).
/// </summary>
public sealed class SpectreReporter(IAnsiConsole console, bool verbose) : IReporter
{
  private IReadOnlyCollection<string> implBehaviors = [];
  private IReadOnlyCollection<string> implVariants = [];

  public void RunStarted(RunContext ctx)
  {
    implBehaviors = ctx.ImplBehaviors;
    implVariants = ctx.ImplVariants;

    var behaviorList = ctx.ImplBehaviors.Count > 0
        ? string.Join(", ", ctx.ImplBehaviors.OrderBy(b => b, StringComparer.Ordinal))
        : "(none)";
    var variantList = ctx.ImplVariants.Count > 0
        ? string.Join(", ", ctx.ImplVariants.OrderBy(v => v, StringComparer.Ordinal))
        : "(none)";

    var header = new Panel(
        new Markup($"[bold]{Markup.Escape(ctx.ImplName)}[/] [dim]{Markup.Escape(ctx.ImplVersion)}[/]\n" +
                   $"[dim]test data:[/] {Markup.Escape(ctx.TestDataPath)}\n" +
                   $"[dim]config:[/]    {Markup.Escape(ctx.ConfigPath)}\n" +
                   $"[dim]tests:[/]     [bold]{ctx.TotalTests}[/] total\n" +
                   $"[dim]behaviors:[/] [green]{Markup.Escape(behaviorList)}[/]\n" +
                   $"[dim]variants:[/]  [green]{Markup.Escape(variantList)}[/]"))
    {
      Header = new PanelHeader("CCL test runner"),
      Border = BoxBorder.Rounded,
      Expand = false,
    };
    console.Write(header);
    console.WriteLine();
  }

  public void TestCompleted(TestOutcome outcome)
  {
    switch (outcome.Kind)
    {
      case OutcomeKind.Passed:
        if (verbose)
          console.MarkupLine($"[green]✓[/] [dim]{Markup.Escape(outcome.Test.Validation)}[/]  {Markup.Escape(outcome.Test.Name)}");
        break;

      case OutcomeKind.Skipped:
        if (verbose)
          console.MarkupLine($"[yellow]○[/] [dim]{Markup.Escape(outcome.Test.Validation)}[/]  {Markup.Escape(outcome.Test.Name)} [dim]— {Markup.Escape(outcome.Reason ?? "")}[/]");
        break;

      case OutcomeKind.Todo:
        if (verbose)
          console.MarkupLine($"[blue]◌[/] [dim]{Markup.Escape(outcome.Test.Validation)}[/]  {Markup.Escape(outcome.Test.Name)} [dim]— {Markup.Escape(outcome.Reason ?? "")}[/]");
        break;

      case OutcomeKind.Failed:
        RenderFailurePanel(outcome);
        break;
    }
  }

  private void RenderFailurePanel(TestOutcome outcome)
  {
    var input = outcome.Test.Inputs.Count > 0 ? outcome.Test.Inputs[0] : "";
    var lines = new List<string>
        {
            $"[red]reason:[/]   {Markup.Escape(outcome.Reason ?? "")}",
        };
    if (!string.IsNullOrEmpty(input))
    {
      lines.Add("[dim]input:[/]");
      lines.Add($"[grey]{Markup.Escape(VisualizeWhitespace(input))}[/]");
    }
    if (outcome.Expected is not null)
    {
      lines.Add("[green]expected:[/]");
      lines.Add($"[green]{Markup.Escape(VisualizeWhitespace(outcome.Expected))}[/]");
    }
    if (outcome.Actual is not null)
    {
      lines.Add("[red]actual:[/]");
      lines.Add($"[red]{Markup.Escape(VisualizeWhitespace(outcome.Actual))}[/]");
    }
    if (outcome.Expected is not null && outcome.Actual is not null && ShouldDiff(outcome.Expected, outcome.Actual))
    {
      lines.Add("[bold]diff:[/] [dim](- expected, + actual)[/]");
      lines.Add(BuildInlineDiffMarkup(outcome.Expected, outcome.Actual));
    }

    var panel = new Panel(new Markup(string.Join("\n", lines)))
    {
      Header = new PanelHeader($"[red]✗[/] [bold]{Markup.Escape(outcome.Test.Validation)}[/] / {Markup.Escape(outcome.Test.Name)}"),
      Border = BoxBorder.Rounded,
      BorderStyle = new Style(Color.Red),
      Expand = false,
    };
    console.Write(panel);
  }

  public void RunCompleted(RunSummary summary)
  {
    console.WriteLine();
    console.Write(BuildValidationTable(summary));
    console.WriteLine();
    console.Write(BuildTagTable(summary, t => t.Functions, "function:", "Function tag"));
    console.WriteLine();
    console.Write(BuildTagTable(summary, t => t.Features, "feature:", "Feature tag"));
    console.WriteLine();
    console.Write(BuildBehaviorTable(summary));
    console.WriteLine();
    console.Write(BuildVariantTable(summary));
    console.WriteLine();

    var status = summary.HasFailures
        ? $"[red bold]{summary.Failed} failed[/]"
        : "[green bold]all green[/]";
    console.MarkupLine(
        $"{status}  •  [green]{summary.Passed} passed[/]  •  [yellow]{summary.Skipped} skipped[/]  •  [blue]{summary.Todo} todo[/]  •  total [bold]{summary.Total}[/]  •  {summary.Duration.TotalMilliseconds:F0} ms");
  }

  private Table BuildBehaviorTable(RunSummary summary)
  {
    var t = new Table()
        .Border(TableBorder.Rounded)
        .Title("[bold]By behavior tag[/] [dim](impl-chosen highlighted)[/]")
        .AddColumn(new TableColumn("Group").NoWrap())
        .AddColumn(new TableColumn("Behavior tag").NoWrap())
        .AddColumn(new TableColumn("[green]Pass[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[red]Fail[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[yellow]Skip[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[blue]Todo[/]").RightAligned().Width(6).NoWrap())
        .AddColumn("Why skipped/todo");

    var byBehavior = summary.Outcomes
        .SelectMany(o => o.Test.Behaviors.Select(tag => (tag, o)))
        .GroupBy(x => x.tag)
        .ToDictionary(g => g.Key, g => g.Select(x => x.o).ToList());

    if (byBehavior.Count == 0)
    {
      t.AddRow("[dim](none)[/]", "[dim](none)[/]", "-", "-", "-", "-", "");
      return t;
    }

    var grouped = byBehavior
        .Select(kv => (Tag: kv.Key, Group: BehaviorGroups.GroupOf(kv.Key), Outcomes: kv.Value))
        .OrderBy(x => x.Group == BehaviorGroups.Other ? 1 : 0)
        .ThenBy(x => x.Group, StringComparer.Ordinal)
        .ThenBy(x => x.Tag, StringComparer.Ordinal)
        .ToList();

    string? lastGroup = null;
    foreach (var (tag, group, outcomes) in grouped)
    {
      var pass = outcomes.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = outcomes.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = outcomes.Count(o => o.Kind == OutcomeKind.Todo);

      var implChose = implBehaviors.Contains(tag);
      var tagCell = implChose
          ? $"[green]behavior:{Markup.Escape(tag)}[/] [green dim]✓[/]"
          : $"[dim]behavior:{Markup.Escape(tag)}[/]";

      var groupCell = group == lastGroup ? "" : $"[dim]{Markup.Escape(group)}[/]";
      lastGroup = group;

      t.AddRow(
          groupCell,
          tagCell,
          pass.ToString(),
          fail > 0 ? $"[red]{fail}[/]" : "0",
          skip > 0 ? $"[yellow]{skip}[/]" : "0",
          todo > 0 ? $"[blue]{todo}[/]" : "0",
          FormatSkipReasons(outcomes));
    }
    return t;
  }

  private Table BuildVariantTable(RunSummary summary)
  {
    var t = new Table()
        .Border(TableBorder.Rounded)
        .Title("[bold]By variant tag[/] [dim](impl-targeted highlighted)[/]")
        .AddColumn(new TableColumn("Variant tag").NoWrap())
        .AddColumn(new TableColumn("[green]Pass[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[red]Fail[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[yellow]Skip[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[blue]Todo[/]").RightAligned().Width(6).NoWrap())
        .AddColumn("Why skipped/todo");

    var grouped = summary.Outcomes
        .SelectMany(o => o.Test.Variants.Select(tag => (tag, o)))
        .GroupBy(x => x.tag)
        .OrderBy(g => g.Key, StringComparer.Ordinal)
        .ToList();

    if (grouped.Count == 0)
    {
      t.AddRow("[dim](none)[/]", "-", "-", "-", "-", "");
      return t;
    }

    foreach (var group in grouped)
    {
      var outcomes = group.Select(x => x.o).ToList();
      var pass = outcomes.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = outcomes.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = outcomes.Count(o => o.Kind == OutcomeKind.Todo);

      var implTargets = implVariants.Contains(group.Key);
      var tagCell = implTargets
          ? $"[green]variant:{Markup.Escape(group.Key)}[/] [green dim]✓[/]"
          : $"[dim]variant:{Markup.Escape(group.Key)}[/]";

      t.AddRow(
          tagCell,
          pass.ToString(),
          fail > 0 ? $"[red]{fail}[/]" : "0",
          skip > 0 ? $"[yellow]{skip}[/]" : "0",
          todo > 0 ? $"[blue]{todo}[/]" : "0",
          FormatSkipReasons(outcomes));
    }
    return t;
  }

  private static Table BuildValidationTable(RunSummary summary)
  {
    var t = new Table()
        .Border(TableBorder.Rounded)
        .Title("[bold]By validation[/]")
        .AddColumn(new TableColumn("Validation").NoWrap())
        .AddColumn(new TableColumn("[green]Pass[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[red]Fail[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[yellow]Skip[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[blue]Todo[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("Total").RightAligned().Width(6).NoWrap())
        .AddColumn("Why skipped/todo");

    foreach (var group in summary.Outcomes.GroupBy(o => o.Test.Validation).OrderBy(g => g.Key))
    {
      var outcomes = group.ToList();
      var pass = outcomes.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = outcomes.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = outcomes.Count(o => o.Kind == OutcomeKind.Todo);
      var total = outcomes.Count;
      t.AddRow(
          Markup.Escape(group.Key),
          pass.ToString(),
          fail > 0 ? $"[red]{fail}[/]" : "0",
          skip > 0 ? $"[yellow]{skip}[/]" : "0",
          todo > 0 ? $"[blue]{todo}[/]" : "0",
          total.ToString(),
          FormatSkipReasons(outcomes));
    }
    return t;
  }

  private static Table BuildTagTable(
      RunSummary summary,
      Func<TestData.CclTestCase, IEnumerable<string>> selector,
      string prefix,
      string label)
  {
    var t = new Table()
        .Border(TableBorder.Rounded)
        .Title($"[bold]By {label.ToLower()}[/]")
        .AddColumn(new TableColumn(label).NoWrap())
        .AddColumn(new TableColumn("[green]Pass[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[red]Fail[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[yellow]Skip[/]").RightAligned().Width(6).NoWrap())
        .AddColumn(new TableColumn("[blue]Todo[/]").RightAligned().Width(6).NoWrap())
        .AddColumn("Why skipped/todo");

    var tagged = summary.Outcomes
        .SelectMany(o => selector(o.Test).Select(tag => (tag, o)))
        .GroupBy(x => x.tag)
        .OrderBy(g => g.Key);

    var any = false;
    foreach (var group in tagged)
    {
      any = true;
      var outcomes = group.Select(x => x.o).ToList();
      var pass = outcomes.Count(o => o.Kind == OutcomeKind.Passed);
      var fail = outcomes.Count(o => o.Kind == OutcomeKind.Failed);
      var skip = outcomes.Count(o => o.Kind == OutcomeKind.Skipped);
      var todo = outcomes.Count(o => o.Kind == OutcomeKind.Todo);
      t.AddRow(
          $"{prefix}{Markup.Escape(group.Key)}",
          pass.ToString(),
          fail > 0 ? $"[red]{fail}[/]" : "0",
          skip > 0 ? $"[yellow]{skip}[/]" : "0",
          todo > 0 ? $"[blue]{todo}[/]" : "0",
          FormatSkipReasons(outcomes));
    }
    if (!any)
    {
      t.AddRow($"[dim](none)[/]", "-", "-", "-", "-", "");
    }
    return t;
  }

  private static string VisualizeWhitespace(string s)
  {
    if (s.Length > 400)
      s = s[..400] + "…";
    return s.Replace("\t", "→···").Replace(" ", "·");
  }

  /// <summary>
  /// Builds a Spectre.Console markup string summarizing the skip+todo reasons
  /// for one rollup row. Most rows have one reason; multiple are joined with
  /// "; " and counted. Returns empty string when nothing was skipped or todoed.
  /// </summary>
  private static string FormatSkipReasons(IEnumerable<TestOutcome> outcomes)
  {
    var byReason = outcomes
        .Where(o => o.Kind is OutcomeKind.Skipped or OutcomeKind.Todo && !string.IsNullOrEmpty(o.Reason))
        .GroupBy(o => (o.Kind, o.Reason!))
        .Select(g => (g.Key.Kind, g.Key.Item2, Count: g.Count()))
        .OrderByDescending(x => x.Count)
        .ThenBy(x => x.Item2, StringComparer.Ordinal)
        .ToList();

    if (byReason.Count == 0)
      return "";

    return string.Join("; ", byReason.Select(x =>
    {
      var color = x.Kind == OutcomeKind.Todo ? "blue" : "yellow";
      return $"[{color}]{Markup.Escape(x.Item2)}[/] [dim]({x.Count})[/]";
    }));
  }

  private static bool ShouldDiff(string expected, string actual)
  {
    return expected.Contains('\n') || actual.Contains('\n');
  }

  private static string BuildInlineDiffMarkup(string expected, string actual)
  {
    var diff = InlineDiffBuilder.Diff(expected, actual);
    var sb = new StringBuilder();
    foreach (var line in diff.Lines)
    {
      var text = Markup.Escape(VisualizeWhitespace(line.Text ?? ""));
      switch (line.Type)
      {
        case ChangeType.Inserted:
          sb.AppendLine($"[green]+ {text}[/]");
          break;
        case ChangeType.Deleted:
          sb.AppendLine($"[red]- {text}[/]");
          break;
        case ChangeType.Modified:
          sb.AppendLine($"[yellow]~ {text}[/]");
          break;
        case ChangeType.Imaginary:
          break;
        default:
          sb.AppendLine($"[grey]  {text}[/]");
          break;
      }
    }
    return sb.ToString().TrimEnd();
  }
}
