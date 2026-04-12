# Ccl.TestRunner

A pluggable .NET test runner for CCL (Categorical Configuration Language)
implementations. It loads the shared `ccl-test-data` JSON suite, runs every
applicable test against your parser, and renders results with
[Spectre.Console](https://spectreconsole.net/) — coloured PASS/FAIL/SKIP
symbols, per-failure panels with input + expected/actual, and end-of-run
summary tables broken down by validation, `function:*` tag, and `feature:*`
tag.

This is the .NET equivalent of `ccl-test-lib` (Go) — any C# / F# / VB
implementation can plug in by implementing one interface.

The reference integration lives in `test/CclDotnet.TestHost/` in this
repository.

## Architecture

```
Ccl.TestRunner.Abstractions   ← your impl references this only
        │
        ▼
Ccl.TestRunner                ← engine: test loading, execution, reporting
        │
        ▼
<YourImpl>.TestHost (exe)     ← tiny host you create per impl
```

`Abstractions` has no third-party dependencies, so adding it to your impl
project will not pull Spectre.Console / YamlDotNet / `System.Text.Json` into
your library's compile graph. Those live one layer up in `Ccl.TestRunner`,
which only your host exe references.

## Integrating a new implementation in 4 steps

The example below uses C#. F# and VB.NET are identical — they implement the
same interfaces.

### 1. Implement the three CCL interfaces

In your library project, add a project (or package) reference to
`Ccl.TestRunner.Abstractions` and implement:

```csharp
using Ccl.TestRunner.Abstractions;

public class MyParser : ICclParser
{
    public IReadOnlyList<Entry> Parse(string input) { /* ... */ }
    public IReadOnlyList<Entry> ParseIndented(string input) { /* ... */ }
    public object BuildHierarchy(string input) { /* ... */ }
    public object Load(string input) { /* ... */ }
    public string Print(string input) { /* ... */ }
    public string CanonicalFormat(string input) { /* ... */ }
}

public class MyProcessor : ICclProcessing { /* Filter, Compose */ }
public class MyTypedAccessor : ICclTypedAccess { /* GetString, GetInt, ... */ }
```

`BuildHierarchy` must return a tree of `IDictionary<string, object>` and
`IList<object>` nodes with leaves as `string`. The runner navigates and
compares against this shape — this is the only structural contract beyond
the interface signatures.

### 2. Write a `ccl-config.yaml`

Place it at the root of your impl repo. It declares which CCL functions and
features your implementation supports — the runner uses this to filter the
test suite to only compatible cases. See `ccl-config.yaml` in this repo for
a complete example.

```yaml
functions:
  - parse
  - build_hierarchy
  - get_string
  # …
features:
  - comments
  - unicode
behaviors:
  - tabs_as_content
  # …
variants:
  - proposed_behavior
```

### 3. Adapter: implement `ICclImplementation`

```csharp
using Ccl.TestRunner.Abstractions;

public sealed class MyImpl : ICclImplementation
{
    public string Name => "ccl-myimpl";
    public string ImplementationVersion =>
        typeof(MyParser).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    // Resolved relative to the host exe; the runner walks up the directory
    // tree looking for it. Repo-root placement is conventional.
    public string ConfigPath => "ccl-config.yaml";

    public ICclParser Parser { get; } = new MyParser();
    public ICclProcessing Processing { get; } = new MyProcessor();
    public ICclTypedAccess TypedAccess { get; } = new MyTypedAccessor();
}
```

### 4. Host exe

Create a console project that references both your impl and `Ccl.TestRunner`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/MyImpl/MyImpl.csproj" />
    <ProjectReference Include="../../tools/Ccl.TestRunner/Ccl.TestRunner.csproj" />
  </ItemGroup>
</Project>
```

`Program.cs` is one line:

```csharp
using Ccl.TestRunner;
return CclTestHost.Run(args, new MyImpl());
```

Then `dotnet run --project test/MyImpl.TestHost --` runs the suite. A
typical justfile recipe:

```just
test *ARGS:
    dotnet run --project test/MyImpl.TestHost -- {{ARGS}}
```

## F# integration

F# implements C# interfaces directly — no extra glue is needed.

```fsharp
open Ccl.TestRunner.Abstractions

type MyFsharpParser() =
    interface ICclParser with
        member _.Parse(input) = (* ... *)
        // ...

type MyFsharpImpl() =
    interface ICclImplementation with
        member _.Name = "ccl-fsharp"
        member _.ImplementationVersion = "0.1.0"
        member _.ConfigPath = "ccl-config.yaml"
        member _.Parser = MyFsharpParser() :> ICclParser
        member _.Processing = MyFsharpProcessor() :> ICclProcessing
        member _.TypedAccess = MyFsharpTypedAccess() :> ICclTypedAccess

[<EntryPoint>]
let main args =
    Ccl.TestRunner.CclTestHost.Run(args, MyFsharpImpl())
```

## Test data discovery

The runner finds the `ccl-test-data` test suite in this order:

1. `--test-data <path>` CLI flag
2. `CCL_TEST_DATA_PATH` environment variable
3. Sibling directory: `../ccl-test-data/generated_tests` (typical workspace
   layout)
4. Local: `<repo>/ccl-test-data/generated_tests`

If none of these resolve, the runner exits with code 2 and a clear message.

## CLI reference

```
<impl-host> [options]
  --test-data <path>     Override test data location
  --validation <name>    Run only tests with this validation type
                         (parse, build_hierarchy, get_string, …)
  --filter <substring>   Run only tests whose name contains <substring>
  --verbose, -v          Show every test outcome (not just failures)
  --pretty | --plain     Force reporter (auto-detected by TTY otherwise)
  --help, -h             Show this help
```

Exit codes: `0` = all green, `1` = at least one failure, `2` = setup error.

## What the runner does for you

- Loads every `*.json` from the test data directory.
- Filters tests against your `ccl-config.yaml` (functions, features,
  variants, conflicts) — tests that need capabilities you don't declare are
  silently excluded, not reported as failures.
- Resolves the composite validations (`compose_associative`, `identity_left`,
  `identity_right`) when you support `parse`, `compose`, and
  `build_hierarchy`.
- Catches assertion mismatches and exceptions thrown by your impl, attaches
  them to the appropriate test, and renders the offending input alongside
  the expected and actual values.
- Auto-detects TTY so output stays readable when piped into a file or CI
  log.

## Adding a new validation type

If `ccl-test-data` introduces a new validation that the runner doesn't yet
dispatch, `Execution/TestExecutor.cs` is where to add it: extend the
`Dispatch` switch and write an `AssertX` method following the existing
pattern. The assertion primitives at the bottom of the file
(`AssertEqual`, `AssertThrowsAny`, etc.) cover everything needed; throwing
a `TestAssertionException` with `expected`/`actual` strings makes the
failure render correctly in both reporters.
