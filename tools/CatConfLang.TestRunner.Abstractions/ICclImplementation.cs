namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Pluggable contract that a .NET CCL implementation provides to the shared test runner.
/// Each implementation hosts its own console exe and passes an instance of this interface
/// to <c>CclTestHost.RunAsync</c>.
/// </summary>
public interface ICclImplementation
{
  /// <summary>Display name of the implementation (e.g. "ccl-dotnet").</summary>
  string Name { get; }

  /// <summary>Free-form version string shown in run headers.</summary>
  string ImplementationVersion { get; }

  /// <summary>
  /// Filesystem path to this implementation's <c>ccl-config.yaml</c>.
  /// May be relative to the host exe; the runner resolves it via standard probing.
  /// </summary>
  string ConfigPath { get; }

  ICclParser Parser { get; }

  ICclProcessing Processing { get; }

  ICclTypedAccess TypedAccess { get; }
}
