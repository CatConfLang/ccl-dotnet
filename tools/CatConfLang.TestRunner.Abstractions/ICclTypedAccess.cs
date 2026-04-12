namespace CatConfLang.TestRunner.Abstractions;

/// <summary>
/// Typed access functions for retrieving values from a CCL hierarchy by path.
/// </summary>
public interface ICclTypedAccess
{
  string GetString(object hierarchy, params string[] pathSegments);
  int GetInt(object hierarchy, params string[] pathSegments);
  bool GetBool(object hierarchy, params string[] pathSegments);
  double GetFloat(object hierarchy, params string[] pathSegments);
  IReadOnlyList<object> GetList(object hierarchy, params string[] pathSegments);
}
