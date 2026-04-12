using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CatConfLang.TestRunner.TestData;

/// <summary>
/// YAML model for ccl-config.yaml.
/// </summary>
public sealed class CclConfigYaml
{
  [YamlMember(Alias = "functions")]
  public List<string> Functions { get; set; } = [];

  [YamlMember(Alias = "features")]
  public List<string> Features { get; set; } = [];

  [YamlMember(Alias = "behaviors")]
  public List<string> Behaviors { get; set; } = [];

  [YamlMember(Alias = "optional_behaviors")]
  public List<string> OptionalBehaviors { get; set; } = [];

  [YamlMember(Alias = "variants")]
  public List<string> Variants { get; set; } = [];
}

public static class CclConfigReader
{
  public static ImplementationConfig LoadFromYaml(string yamlPath)
  {
    var yaml = File.ReadAllText(yamlPath);
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    var config = deserializer.Deserialize<CclConfigYaml>(yaml);

    return new ImplementationConfig
    {
      Functions = [.. config.Functions],
      Features = [.. config.Features],
      Behaviors = [.. config.Behaviors],
      Variants = [.. config.Variants],
    };
  }
}
