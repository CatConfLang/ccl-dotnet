using System.Diagnostics;
using CatConfLang.TestRunner.Abstractions;
using CatConfLang.TestRunner.Execution;
using CatConfLang.TestRunner.Reporting;
using CatConfLang.TestRunner.TestData;
using Spectre.Console;

namespace CatConfLang.TestRunner;

/// <summary>
/// Entry point used by each implementation's host exe. Parses CLI args,
/// loads test data, executes tests, and renders results.
/// </summary>
public static class CclTestHost
{
  /// <summary>
  /// Runs the CCL test suite against <paramref name="impl"/>. Returns the process
  /// exit code (0 = all green, 1 = at least one failure, 2 = setup error).
  /// </summary>
  public static int Run(string[] args, ICclImplementation impl)
  {
    CliOptions options;
    try
    {
      options = CliOptions.Parse(args);
    }
    catch (ArgumentException ex)
    {
      Console.Error.WriteLine($"error: {ex.Message}");
      CliOptions.PrintUsage(Console.Error);
      return 2;
    }

    if (options.ShowHelp)
    {
      CliOptions.PrintUsage(Console.Out);
      return 0;
    }

    try
    {
      var testDataPath = TestDataPathResolver.ResolveTestDataPath(options.TestDataPath);
      var configPath = TestDataPathResolver.ResolveConfigPath(impl.ConfigPath);
      var config = CclConfigReader.LoadFromYaml(configPath);
      var allTests = TestDataLoader.LoadAllTests(testDataPath);

      var filtered = allTests.AsEnumerable();
      if (options.Validation is not null)
        filtered = filtered.Where(t => t.Validation == options.Validation);
      if (options.NameFilter is not null)
        filtered = filtered.Where(t => t.Name.Contains(options.NameFilter, StringComparison.OrdinalIgnoreCase));

      var tests = filtered.ToList();

      var reporter = SelectReporter(options, verbose: options.Verbose);
      reporter.RunStarted(new RunContext(
          impl.Name,
          impl.ImplementationVersion,
          testDataPath,
          configPath,
          tests.Count,
          options.Verbose,
          [.. config.Behaviors],
          [.. config.Variants]));

      var outcomes = new List<TestOutcome>(tests.Count);
      var sw = Stopwatch.StartNew();
      foreach (var test in tests)
      {
        var compat = TestDataLoader.Classify(test, config);
        var outcome = compat.Kind switch
        {
          TestDataLoader.CompatibilityKind.Todo => TestOutcome.Todo(test, compat.Reason ?? ""),
          TestDataLoader.CompatibilityKind.Skip => TestOutcome.Skip(test, compat.Reason ?? ""),
          _ => TestExecutor.Run(impl, test),
        };
        outcomes.Add(outcome);
        reporter.TestCompleted(outcome);
      }
      sw.Stop();

      var summary = new RunSummary(outcomes, sw.Elapsed);
      reporter.RunCompleted(summary);

      if (options.ResultsPath is not null)
      {
        JsonResultsWriter.Write(
            options.ResultsPath,
            impl,
            config,
            testDataPath,
            outcomes);
      }

      return summary.HasFailures ? 1 : 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"fatal: {ex.Message}");
      Console.Error.WriteLine(ex.StackTrace);
      return 2;
    }
  }

  private static IReporter SelectReporter(CliOptions options, bool verbose)
  {
    var wantPretty = options.ReporterMode switch
    {
      ReporterMode.Pretty => true,
      ReporterMode.Plain => false,
      _ => AnsiConsole.Profile.Capabilities.Ansi && !Console.IsOutputRedirected,
    };

    return wantPretty
        ? new SpectreReporter(AnsiConsole.Console, verbose)
        : new PlainReporter(Console.Out, verbose);
  }
}

internal enum ReporterMode { Auto, Pretty, Plain }

internal sealed record CliOptions(
    string? TestDataPath,
    string? Validation,
    string? NameFilter,
    bool Verbose,
    ReporterMode ReporterMode,
    string? ResultsPath,
    bool ShowHelp)
{
  public static CliOptions Parse(string[] args)
  {
    string? testData = null;
    string? validation = null;
    string? filter = null;
    var verbose = false;
    var mode = ReporterMode.Auto;
    string? resultsPath = null;
    var help = false;

    for (var i = 0; i < args.Length; i++)
    {
      var a = args[i];
      switch (a)
      {
        case "--test-data":
          testData = RequireValue(args, ref i, "--test-data"); break;
        case "--validation":
          validation = RequireValue(args, ref i, "--validation"); break;
        case "--filter":
          filter = RequireValue(args, ref i, "--filter"); break;
        case "--verbose" or "-v":
          verbose = true; break;
        case "--pretty":
          mode = ReporterMode.Pretty; break;
        case "--plain":
          mode = ReporterMode.Plain; break;
        case "--results":
          resultsPath = RequireValue(args, ref i, "--results"); break;
        case "--help" or "-h":
          help = true; break;
        default:
          throw new ArgumentException($"unknown argument: {a}");
      }
    }

    return new CliOptions(testData, validation, filter, verbose, mode, resultsPath, help);
  }

  private static string RequireValue(string[] args, ref int i, string flag)
  {
    if (i + 1 >= args.Length)
      throw new ArgumentException($"{flag} requires a value");
    return args[++i];
  }

  public static void PrintUsage(TextWriter w)
  {
    w.WriteLine("Usage: <impl-host> [options]");
    w.WriteLine("Options:");
    w.WriteLine("  --test-data <path>     Override test data location");
    w.WriteLine("  --validation <name>    Run only tests with this validation type");
    w.WriteLine("  --filter <substring>   Run only tests whose name contains <substring>");
    w.WriteLine("  --verbose, -v          Show every test outcome (not just failures)");
    w.WriteLine("  --pretty | --plain     Force reporter (auto-detect by default)");
    w.WriteLine("  --results <path>       Write machine-readable JSON results (test-results-format v1.1.0)");
    w.WriteLine("  --help, -h             Show this help");
  }
}
