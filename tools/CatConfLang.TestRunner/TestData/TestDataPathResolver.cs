namespace CatConfLang.TestRunner.TestData;

/// <summary>
/// Resolves the test data directory and ccl-config.yaml location, mirroring the
/// probe order used by the original xUnit-based fixture: explicit override,
/// CCL_TEST_DATA_PATH env var, sibling/local <c>ccl-test-data/generated_tests</c>.
/// </summary>
public static class TestDataPathResolver
{
  public static string ResolveTestDataPath(string? explicitPath = null)
  {
    if (!string.IsNullOrEmpty(explicitPath))
    {
      if (Directory.Exists(explicitPath))
        return explicitPath;

      var generated = Path.Combine(explicitPath, "generated_tests");
      if (Directory.Exists(generated))
        return generated;

      throw new DirectoryNotFoundException($"Test data path not found: {explicitPath}");
    }

    var envPath = Environment.GetEnvironmentVariable("CCL_TEST_DATA_PATH");
    if (!string.IsNullOrEmpty(envPath))
    {
      if (Directory.Exists(envPath))
        return envPath;

      var envGenerated = Path.Combine(envPath, "generated_tests");
      if (Directory.Exists(envGenerated))
        return envGenerated;
    }

    var dir = AppContext.BaseDirectory;
    while (dir is not null)
    {
      var parent = Path.GetDirectoryName(dir);
      if (parent is not null)
      {
        var siblingTestData = Path.Combine(parent, "ccl-test-data", "generated_tests");
        if (Directory.Exists(siblingTestData))
          return siblingTestData;
      }

      var configPath = Path.Combine(dir, "ccl-config.yaml");
      if (File.Exists(configPath))
      {
        var localTestData = Path.Combine(dir, "ccl-test-data", "generated_tests");
        if (Directory.Exists(localTestData))
          return localTestData;

        var repoParent = Path.GetDirectoryName(dir);
        if (repoParent is not null)
        {
          var siblingFromRoot = Path.Combine(repoParent, "ccl-test-data", "generated_tests");
          if (Directory.Exists(siblingFromRoot))
            return siblingFromRoot;
        }
      }

      dir = Path.GetDirectoryName(dir);
    }

    throw new DirectoryNotFoundException(
        "CCL test data not found. Set CCL_TEST_DATA_PATH environment variable or ensure " +
        "ccl-test-data repo is a sibling directory.");
  }

  /// <summary>
  /// If <paramref name="configPath"/> is absolute and exists, returns it. Otherwise walks up
  /// from the host exe's base directory looking for the file.
  /// </summary>
  public static string ResolveConfigPath(string configPath)
  {
    if (Path.IsPathRooted(configPath) && File.Exists(configPath))
      return configPath;

    if (File.Exists(configPath))
      return Path.GetFullPath(configPath);

    var dir = AppContext.BaseDirectory;
    while (dir is not null)
    {
      var candidate = Path.Combine(dir, configPath);
      if (File.Exists(candidate))
        return candidate;
      dir = Path.GetDirectoryName(dir);
    }

    throw new FileNotFoundException($"ccl-config.yaml not found: {configPath}");
  }
}
