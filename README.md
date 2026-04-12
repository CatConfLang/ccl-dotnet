# ccl-dotnet

A .NET implementation of CCL (Categorical Configuration Language).

- **`src/CclDotnet/`** — the parser, processor, and typed accessor
  (`CclParser`, `CclProcessor`, `CclTypedAccessor`).
- **`tools/Ccl.TestRunner.Abstractions/`** — pluggable interfaces
  (`ICclImplementation`, `ICclParser`, `ICclProcessing`, `ICclTypedAccess`,
  `Entry`) shared across .NET CCL implementations.
- **`tools/Ccl.TestRunner/`** — the cross-impl test runner that consumes
  `ccl-test-data`. Renders results with Spectre.Console.
  See **[`tools/Ccl.TestRunner/README.md`](tools/Ccl.TestRunner/README.md)**
  for how to integrate another .NET implementation (C#, F#, VB).
- **`test/CclDotnet.TestHost/`** — the host exe that wires `CclDotnet`
  into the runner. Reference integration for new impls.

## Common commands

```bash
just build                    # dotnet build
just test                     # run the full CCL test suite
just test --pretty            # force coloured output
just test --validation parse  # run only one validation type
just test --filter foo        # run tests whose name contains "foo"
just test --verbose           # show every test outcome, not just failures
just test-validation parse    # alias for --validation
just lint                     # dotnet format --verify-no-changes
just ci                       # format, lint, build, test
```

`just test` runs the test runner host, not `dotnet test`. See the runner
README for the full CLI surface.
