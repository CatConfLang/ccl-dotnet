using CatConfLang.TestRunner.Abstractions;
using CclDotnet;

namespace CclDotnet.TestHost;

/// <summary>
/// Adapts the CclDotnet parser/processor/typed-accessor to the runner's plug-in
/// contract. Each .NET CCL implementation provides its own equivalent host project.
/// </summary>
public sealed class CclDotnetImplementation : ICclImplementation
{
  public string Name => "ccl-dotnet";

  public string ImplementationVersion =>
      typeof(CclParser).Assembly.GetName().Version?.ToString() ?? "0.0.0";

  public string ConfigPath => "ccl-config.yaml";

  public ICclParser Parser { get; } = new CclParser();
  public ICclProcessing Processing { get; } = new CclProcessor();
  public ICclTypedAccess TypedAccess { get; } = new CclTypedAccessor();
}
