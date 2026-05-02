using System.Diagnostics;
using System.Text.Json;
using CatConfLang.TestRunner.Abstractions;
using CatConfLang.TestRunner.TestData;

namespace CatConfLang.TestRunner.Execution;

/// <summary>
/// Runs a single CCL test case against an implementation. The dispatch and assertion
/// logic is a port of the original xUnit-based CclAssertions, with Assert.* replaced
/// by explicit checks that record structured expected/actual strings.
/// </summary>
public static class TestExecutor
{
  public static TestOutcome Run(ICclImplementation impl, CclTestCase test)
  {
    var sw = Stopwatch.StartNew();
    try
    {
      Dispatch(impl, test);
      return TestOutcome.Pass(test, sw.Elapsed);
    }
    catch (SkipException skip)
    {
      return TestOutcome.Skip(test, skip.Message);
    }
    catch (TestAssertionException ex)
    {
      return TestOutcome.Fail(test, ex.Message, ex.Expected, ex.Actual, ex, sw.Elapsed);
    }
    catch (Exception ex)
    {
      return TestOutcome.Fail(test, $"Unexpected exception: {ex.GetType().Name}: {ex.Message}", null, null, ex, sw.Elapsed);
    }
  }

  private static void Dispatch(ICclImplementation impl, CclTestCase test)
  {
    switch (test.Validation)
    {
      case "parse": AssertParse(impl, test); break;
      case "parse_indented": AssertParseIndented(impl, test); break;
      case "build_hierarchy": AssertBuildHierarchy(impl, test); break;
      case "build_model": AssertBuildModel(impl, test); break;
      case "load": AssertLoad(impl, test); break;
      case "print": AssertPrint(impl, test); break;
      case "canonical_format": AssertCanonicalFormat(impl, test); break;
      case "round_trip": AssertRoundTrip(impl, test); break;
      case "get_string": AssertGetString(impl, test); break;
      case "get_int": AssertGetInt(impl, test); break;
      case "get_bool": AssertGetBool(impl, test); break;
      case "get_float": AssertGetFloat(impl, test); break;
      case "get_list": AssertGetList(impl, test); break;
      case "filter": AssertFilter(impl, test); break;
      case "compose": AssertCompose(impl, test); break;
      case "compose_associative": AssertComposeAssociative(impl, test); break;
      case "identity_left": AssertIdentityLeft(impl, test); break;
      case "identity_right": AssertIdentityRight(impl, test); break;
      default:
        throw new SkipException($"Unsupported validation type: {test.Validation}");
    }
  }

  private static void AssertParse(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    if (test.Expected.Error || test.ExpectError)
    {
      AssertThrowsAny(() => impl.Parser.Parse(input), "Parse should throw");
      return;
    }

    var entries = impl.Parser.Parse(input);
    AssertEqual(test.Expected.Count, entries.Count, "entry count");

    if (test.Expected.Entries is { Count: > 0 })
    {
      AssertEqual(test.Expected.Entries.Count, entries.Count, "entry count");
      for (var i = 0; i < test.Expected.Entries.Count; i++)
      {
        AssertEntryEqual(
            test.Expected.Entries[i].Key, test.Expected.Entries[i].Value,
            entries[i].Key, entries[i].Value,
            $"entries[{i}]");
      }
    }
  }

  private static void AssertParseIndented(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    if (test.Expected.Error || test.ExpectError)
    {
      AssertThrowsAny(() => impl.Parser.ParseIndented(input), "ParseIndented should throw");
      return;
    }

    var entries = impl.Parser.ParseIndented(input);
    AssertEqual(test.Expected.Count, entries.Count, "entry count");

    if (test.Expected.Entries is { Count: > 0 })
    {
      AssertEqual(test.Expected.Entries.Count, entries.Count, "entry count");
      for (var i = 0; i < test.Expected.Entries.Count; i++)
      {
        AssertEntryEqual(
            test.Expected.Entries[i].Key, test.Expected.Entries[i].Value,
            entries[i].Key, entries[i].Value,
            $"entries[{i}]");
      }
    }
  }

  private static void AssertBuildHierarchy(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    if (test.Expected.Error || test.ExpectError)
    {
      AssertThrowsAny(() => impl.Parser.BuildHierarchy(input), "BuildHierarchy should throw");
      return;
    }

    var result = impl.Parser.BuildHierarchy(input);
    AssertNotNull(result, "BuildHierarchy result");

    if (test.Expected.Object is JsonElement expectedObj)
    {
      AssertObjectMatches(expectedObj, result);
    }
  }

  private static void AssertBuildModel(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    if (test.Expected.Error || test.ExpectError)
    {
      AssertThrowsAny(() => impl.Parser.BuildModel(input), "BuildModel should throw");
      return;
    }

    var result = impl.Parser.BuildModel(input);
    AssertNotNull(result, "BuildModel result");

    if (test.Expected.Object is JsonElement expectedObj)
    {
      AssertObjectMatches(expectedObj, result);
    }
  }

  private static void AssertLoad(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    if (test.Expected.Error || test.ExpectError)
    {
      AssertThrowsAny(() => impl.Parser.Load(input), "Load should throw");
      return;
    }

    var result = impl.Parser.Load(input);
    AssertNotNull(result, "Load result");

    if (test.Expected.Object is JsonElement expectedObj)
    {
      AssertObjectMatches(expectedObj, result);
    }
  }

  private static void AssertPrint(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var result = impl.Parser.Print(input);

    if (test.Expected.Value is JsonElement valElem && valElem.ValueKind == JsonValueKind.String)
    {
      AssertEqual(valElem.GetString(), result, "print output");
    }
    else if (test.Expected.Text is not null)
    {
      AssertEqual(test.Expected.Text, result, "print output");
    }
  }

  private static void AssertCanonicalFormat(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var result = impl.Parser.CanonicalFormat(input);

    if (test.Expected.Value is JsonElement valElem && valElem.ValueKind == JsonValueKind.String)
    {
      AssertEqual(valElem.GetString(), result, "canonical_format output");
    }
    else if (test.Expected.Text is not null)
    {
      AssertEqual(test.Expected.Text, result, "canonical_format output");
    }
  }

  private static void AssertRoundTrip(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];

    var entries = impl.Parser.Parse(input);
    var printed = impl.Parser.Print(input);
    var reparsed = impl.Parser.Parse(printed);

    AssertEqual(entries.Count, reparsed.Count, "entry count after round-trip");
    for (var i = 0; i < entries.Count; i++)
    {
      AssertEntryEqual(
          entries[i].Key, entries[i].Value,
          reparsed[i].Key, reparsed[i].Value,
          $"reparsed[{i}]");
    }

    if (test.Expected.Value is JsonElement valElem && valElem.ValueKind == JsonValueKind.String)
    {
      AssertEqual(valElem.GetString(), printed, "printed text");
    }
  }

  private static bool IsTypedAccessErrorTest(CclTestCase test, bool isList = false)
  {
    if (test.Expected.Error || test.ExpectError)
      return true;
    if (isList && test.Expected.Count == 0 && test.Expected.List is null)
      return true;
    if (!isList && test.Expected.Value is null &&
        (test.Name.Contains("error", StringComparison.OrdinalIgnoreCase) ||
         test.Name.Contains("mismatch", StringComparison.OrdinalIgnoreCase)))
      return true;
    return false;
  }

  private static void AssertGetString(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var hierarchy = impl.Parser.BuildHierarchy(input);
    var pathSegments = test.Args!.ToArray();

    if (IsTypedAccessErrorTest(test))
    {
      AssertThrowsAny(() => impl.TypedAccess.GetString(hierarchy, pathSegments), "GetString should throw");
      return;
    }

    var result = impl.TypedAccess.GetString(hierarchy, pathSegments);

    if (test.Expected.Value is JsonElement valElem && valElem.ValueKind == JsonValueKind.String)
    {
      AssertEqual(valElem.GetString(), result, "get_string");
    }
  }

  private static void AssertGetInt(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var hierarchy = impl.Parser.BuildHierarchy(input);
    var pathSegments = test.Args!.ToArray();

    if (IsTypedAccessErrorTest(test))
    {
      AssertThrowsAny(() => impl.TypedAccess.GetInt(hierarchy, pathSegments), "GetInt should throw");
      return;
    }

    var result = impl.TypedAccess.GetInt(hierarchy, pathSegments);

    if (test.Expected.Value is JsonElement valElem)
    {
      var expected = valElem.ValueKind == JsonValueKind.Number
          ? valElem.GetInt32()
          : int.Parse(valElem.GetString()!);
      AssertEqual(expected, result, "get_int");
    }
  }

  private static void AssertGetBool(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var hierarchy = impl.Parser.BuildHierarchy(input);
    var pathSegments = test.Args!.ToArray();

    if (IsTypedAccessErrorTest(test))
    {
      AssertThrowsAny(() => impl.TypedAccess.GetBool(hierarchy, pathSegments), "GetBool should throw");
      return;
    }

    var result = impl.TypedAccess.GetBool(hierarchy, pathSegments);

    if (test.Expected.Value is JsonElement valElem)
    {
      var expected = valElem.ValueKind == JsonValueKind.True || valElem.ValueKind == JsonValueKind.False
          ? valElem.GetBoolean()
          : bool.Parse(valElem.GetString()!);
      AssertEqual(expected, result, "get_bool");
    }
  }

  private static void AssertGetFloat(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var hierarchy = impl.Parser.BuildHierarchy(input);
    var pathSegments = test.Args!.ToArray();

    if (IsTypedAccessErrorTest(test))
    {
      AssertThrowsAny(() => impl.TypedAccess.GetFloat(hierarchy, pathSegments), "GetFloat should throw");
      return;
    }

    var result = impl.TypedAccess.GetFloat(hierarchy, pathSegments);

    if (test.Expected.Value is JsonElement valElem)
    {
      var expected = valElem.ValueKind == JsonValueKind.Number
          ? valElem.GetDouble()
          : double.Parse(valElem.GetString()!);
      AssertEqualFloat(expected, result, "get_float");
    }
  }

  private static void AssertGetList(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var hierarchy = impl.Parser.BuildHierarchy(input);
    var pathSegments = test.Args!.ToArray();

    if (IsTypedAccessErrorTest(test, isList: true))
    {
      AssertThrowsAny(() => impl.TypedAccess.GetList(hierarchy, pathSegments), "GetList should throw");
      return;
    }

    var result = impl.TypedAccess.GetList(hierarchy, pathSegments);

    if (test.Expected.List is JsonElement listElem && listElem.ValueKind == JsonValueKind.Array)
    {
      var expectedList = listElem.EnumerateArray()
          .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText())
          .ToList();

      AssertEqual(expectedList.Count, result.Count, "list length");
      for (var i = 0; i < expectedList.Count; i++)
      {
        if (result[i] is Dictionary<string, object> || result[i] is List<object>)
        {
          var actualJson = JsonSerializer.Serialize(result[i]);
          var expectedNormalized = NormalizeJson(expectedList[i]);
          AssertEqual(expectedNormalized, actualJson, $"list[{i}]");
        }
        else
        {
          AssertEqual(expectedList[i], result[i]?.ToString(), $"list[{i}]");
        }
      }
    }
  }

  private static void AssertFilter(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var entries = impl.Parser.Parse(input);
    var predicate = test.Predicate!;

    var result = impl.Processing.Filter(entries, predicate.Field, predicate.Op, predicate.Value);
    AssertEqual(test.Expected.Count, result.Count, "filtered count");

    if (test.Expected.Entries is { Count: > 0 })
    {
      for (var i = 0; i < test.Expected.Entries.Count; i++)
      {
        AssertEntryEqual(
            test.Expected.Entries[i].Key, test.Expected.Entries[i].Value,
            result[i].Key, result[i].Value,
            $"result[{i}]");
      }
    }
  }

  private static void AssertCompose(ICclImplementation impl, CclTestCase test)
  {
    var inputs = test.Inputs.ToArray();
    var result = impl.Processing.Compose(inputs);
    AssertEqual(test.Expected.Count, result.Count, "composed count");

    if (test.Expected.Entries is { Count: > 0 })
    {
      for (var i = 0; i < test.Expected.Entries.Count; i++)
      {
        AssertEntryEqual(
            test.Expected.Entries[i].Key, test.Expected.Entries[i].Value,
            result[i].Key, result[i].Value,
            $"result[{i}]");
      }
    }
  }

  private static void AssertComposeAssociative(ICclImplementation impl, CclTestCase test)
  {
    var inputs = test.Inputs.ToArray();
    if (inputs.Length < 3)
      throw new TestAssertionException("compose_associative requires at least 3 inputs");

    var leftFirst = impl.Processing.Compose(inputs[0], inputs[1]);
    var leftPrinted = EntriesToString(leftFirst);
    var leftResult = impl.Processing.Compose(leftPrinted, inputs[2]);

    var rightFirst = impl.Processing.Compose(inputs[1], inputs[2]);
    var rightPrinted = EntriesToString(rightFirst);
    var rightResult = impl.Processing.Compose(inputs[0], rightPrinted);

    AssertEqual(leftResult.Count, rightResult.Count, "associative count");
    for (var i = 0; i < leftResult.Count; i++)
    {
      AssertEntryEqual(
          leftResult[i].Key, leftResult[i].Value,
          rightResult[i].Key, rightResult[i].Value,
          $"assoc[{i}]");
    }
  }

  private static void AssertIdentityLeft(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var result = impl.Processing.Compose("", input);
    var expected = impl.Parser.Parse(input);

    AssertEqual(expected.Count, result.Count, "identity_left count");
    for (var i = 0; i < expected.Count; i++)
    {
      AssertEntryEqual(
          expected[i].Key, expected[i].Value,
          result[i].Key, result[i].Value,
          $"id_left[{i}]");
    }
  }

  private static void AssertIdentityRight(ICclImplementation impl, CclTestCase test)
  {
    var input = test.Inputs[0];
    var result = impl.Processing.Compose(input, "");
    var expected = impl.Parser.Parse(input);

    AssertEqual(expected.Count, result.Count, "identity_right count");
    for (var i = 0; i < expected.Count; i++)
    {
      AssertEntryEqual(
          expected[i].Key, expected[i].Value,
          result[i].Key, result[i].Value,
          $"id_right[{i}]");
    }
  }

  private static void AssertObjectMatches(JsonElement expected, object actual)
  {
    switch (expected.ValueKind)
    {
      case JsonValueKind.Object:
        if (actual is not IDictionary<string, object> dict)
          throw new TestAssertionException(
              $"Expected object, got {actual?.GetType().Name ?? "null"}",
              expected.GetRawText(),
              actual?.ToString());
        foreach (var prop in expected.EnumerateObject())
        {
          if (!dict.ContainsKey(prop.Name))
            throw new TestAssertionException(
                $"Missing key: {prop.Name}",
                expected.GetRawText(),
                JsonSerializer.Serialize(actual));
          AssertObjectMatches(prop.Value, dict[prop.Name]);
        }
        AssertEqual(expected.EnumerateObject().Count(), dict.Count, "object key count");
        break;

      case JsonValueKind.Array:
        if (actual is not IList<object> list)
          throw new TestAssertionException(
              $"Expected array, got {actual?.GetType().Name ?? "null"}",
              expected.GetRawText(),
              actual?.ToString());
        var expectedArray = expected.EnumerateArray().ToList();
        AssertEqual(expectedArray.Count, list.Count, "array length");
        for (var i = 0; i < expectedArray.Count; i++)
        {
          AssertObjectMatches(expectedArray[i], list[i]);
        }
        break;

      case JsonValueKind.String:
        AssertEqual(expected.GetString(), actual?.ToString(), "string value");
        break;

      case JsonValueKind.Number:
        AssertEqualFloat(expected.GetDouble(), Convert.ToDouble(actual), "number value");
        break;

      case JsonValueKind.True:
      case JsonValueKind.False:
        AssertEqual(expected.GetBoolean(), Convert.ToBoolean(actual), "boolean value");
        break;

      default:
        throw new TestAssertionException($"Unexpected JSON value kind: {expected.ValueKind}");
    }
  }

  private static string EntriesToString(IReadOnlyList<Entry> entries)
  {
    return string.Join("\n", entries.Select(e =>
        string.IsNullOrEmpty(e.Value) ? $"{e.Key} =" : $"{e.Key} = {e.Value}"));
  }

  private static string NormalizeJson(string json)
  {
    var doc = JsonDocument.Parse(json);
    return JsonSerializer.Serialize(doc.RootElement);
  }

  // ----- Assertion primitives -----

  private static void AssertEqual<T>(T expected, T actual, string what)
  {
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
      throw new TestAssertionException(
          $"Mismatch in {what}",
          Render(expected),
          Render(actual));
    }
  }

  private static void AssertEntryEqual(
      string expectedKey, string expectedValue,
      string actualKey, string actualValue,
      string what)
  {
    if (expectedKey == actualKey && expectedValue == actualValue)
      return;

    var diffField = expectedKey != actualKey ? "key" : "value";
    throw new TestAssertionException(
        $"Mismatch in {what} ({diffField})",
        FormatEntry(expectedKey, expectedValue),
        FormatEntry(actualKey, actualValue));
  }

  private static string FormatEntry(string key, string value) =>
      $"({key}, {value})";

  private static void AssertEqualFloat(double expected, double actual, string what)
  {
    if (Math.Abs(expected - actual) > 1e-10)
    {
      throw new TestAssertionException(
          $"Mismatch in {what}",
          expected.ToString("R"),
          actual.ToString("R"));
    }
  }

  private static void AssertNotNull(object? value, string what)
  {
    if (value is null)
      throw new TestAssertionException($"{what} was null");
  }

  private static void AssertThrowsAny(Action action, string what)
  {
    try
    {
      action();
    }
    catch
    {
      return;
    }
    throw new TestAssertionException($"{what} but did not");
  }

  private static string Render(object? value) => value switch
  {
    null => "<null>",
    string s => s,
    _ => value.ToString() ?? "<null>",
  };
}
