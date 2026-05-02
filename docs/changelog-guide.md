# ccl-dotnet Changelog Fragment Guide

Canonical instructions for creating [changie](https://changie.dev) changelog fragments in this repo. Both the Claude Code skill (`.claude/skills/ccl-dotnet-changelog/SKILL.md`) and the GitHub Copilot instruction file (`.github/instructions/changelog.instructions.md`) point here, so edit this file when the rules change and the rest will follow.

## Repo facts you can rely on

These come straight from `.changie.yaml` and the existing fragments in `.changes/unreleased/`. If `.changie.yaml` has been edited and these no longer match, trust the file and update this guide afterward.

### Projects

| Project key | Source path | What it is |
|---|---|---|
| `ccldotnet` | `src/CclDotnet/` | The C# parser, processor, typed accessor (`CclParser`, `CclProcessor`, `CclTypedAccessor`). |
| `pacman` | `src/CclDotnet.Pacman/` | The F# Pacman parser implementation (`CclPacmanParser`, etc.). |
| `abstractions` | `tools/CatConfLang.TestRunner.Abstractions/` | The shared `ICclImplementation` / `ICclParser` / `ICclProcessing` / `ICclTypedAccess` / `Entry` interfaces. |
| `testrunner` | `tools/CatConfLang.TestRunner/` | The cross-impl test runner (Spectre.Console UI, ccl-test-data consumer). |

The `test/` directory holds test hosts that exercise the projects above. A change confined to `test/CclDotnet.TestHost/` belongs with `ccldotnet`; a change in `test/CclDotnet.Pacman.TestHost/` belongs with `pacman`. Pure plumbing changes to a TestHost (rewiring it after an interface change) usually do not need their own fragment unless they affect how integrators wire up a new implementation — in that case, attach the fragment to `testrunner` or `abstractions` depending on what actually changed.

Solution-wide changes (`Directory.Build.props`, `CclDotnet.sln`, `mise.toml`, `justfile`, root `README.md`, CI) are generally not changelog-worthy on their own. Only write a fragment if the change is user-visible — for example, a new `just` recipe that integrators are expected to run, or a `Directory.Build.props` change that alters published package metadata. In that case attach it to whichever project's package surface is affected.

### Kinds

```
Added         (auto: minor)
Fixed         (auto: patch)
Performance   (auto: patch)
Changed       (auto: patch)
Reverted      (auto: patch)
Dependencies  (auto: patch)
Security      (auto: patch)
```

There is no `Breaking` kind here. A breaking change is recorded as `Changed` with the breakage and migration path called out in the body.

Mapping from conventional-commit prefixes:

| Commit prefix | Kind |
|---|---|
| `feat:` | Added |
| `fix:` | Fixed |
| `perf:` | Performance |
| `refactor:` | Changed |
| `revert:` | Reverted |
| `deps:` / dependency bumps | Dependencies |
| `security:` | Security |

## Workflow

### 1. Confirm you are in ccl-dotnet

The repo root must contain `.changie.yaml` with the four project keys listed above. If not, you're either in the wrong working directory or the config has drifted — stop and tell the user.

### 2. Find what changed

If the user already described the change in plain language ("add a changelog entry for the canonical formatter fix in pacman"), use that and skip the diff. Otherwise:

```bash
git merge-base HEAD main          # divergence point — call this BASE
git log BASE..HEAD --oneline      # commits on the branch
git diff BASE..HEAD --stat        # files touched, grouped by area
```

Read commit messages first; they are usually enough. Only read the diff for a commit when its message is too terse to describe the change for a changelog reader.

### 3. Decide which project(s) each change belongs to

Walk the changed paths and bucket them by project using the table above. A single logical change can span multiple projects — for example, adding a new method to `ICclParser` (abstractions) and implementing it in both parsers (ccldotnet, pacman). In that case write **one fragment per affected project**, each describing the change from that project's user-facing perspective.

When you are unsure, ask the user. Misattributing a fragment to the wrong project means it lands in the wrong package's CHANGELOG.md at release time, which is annoying to undo.

### 4. Group commits into logical changes

Combine related commits of the same kind into a single fragment when a reader would be better served by one entry. Keep them separate when they address distinct things. A `fix:` for a parser bug and a `fix:` for a test-runner argv bug stay separate even though both are "Fixed" — they belong to different projects anyway.

Skip these from the changelog:
- `chore:` (CI, formatting, dependency lockfile churn that doesn't change the resolved version)
- `docs:` unless the doc change is user-facing (e.g., a new section in `tools/CatConfLang.TestRunner/README.md` that integrators need)
- `test:` unless tests reflect a real behavior change worth advertising
- merge / revert-of-revert / WIP commits

### 5. Check for existing fragments

```bash
ls .changes/unreleased/
```

Read any existing fragments. If one already covers a change you were going to write, skip it and tell the user. Don't create duplicates and don't rewrite someone else's entry without being asked.

### 6. Write the fragments

**All fragments go in `.changes/unreleased/`**, never in per-project subdirectories under `.changes/`. The subdirectories there hold released versions; CI does not pick up unreleased entries placed in them.

**Filename**: `<project-key>-<Kind>-<YYYYMMDD>-<HHMMSS>.yaml`

Use the current local time. If two fragments would otherwise collide on filename (same project, same kind, same second), bump the seconds field on the second one.

**Body content**: ccl-dotnet fragments use a **single-line plain `body:` string**, not a block scalar. Look at the existing fragments — `body: Add the F# Pacman parser implementation with parse, parse_indented, build_model, print, and canonical_format support.` — and match that shape. If you genuinely need multiple paragraphs, fall back to a YAML block scalar, but the strong default here is one line.

**Field order** (match the existing fragments exactly):

```yaml
project: <project-key>
kind: <Kind label as in .changie.yaml>
body: <single-line summary of the change>
time: <ISO 8601 with nanosecond precision and timezone offset>
```

The easiest way to use changie itself for the timestamp and field order is `changie new --dry-run -j <project-key> -k <Kind> -b "<body>"` — but writing the file directly is fine and is what the existing fragments look like. Either way the output should match the format above byte-for-byte (apart from body and time).

**Writing good bodies for this repo:**

- Lead with what the user of that package gets, not what the diff did. Compare:
  - Bad: "Refactor `CclProcessor.Compose` to share a helper with `CclPacmanProcessor`."
  - Good: "Share scalar-to-list coercion logic between the C# and Pacman processors so both implementations agree on edge cases."
- Name the public API surface when it changed (`CclParser`, `ICclParser.ParseIndented`, `--validation` flag, etc.). Readers of the changelog scan for symbol names.
- For `pacman` and `ccldotnet`, focus on parser/processor behavior and the public types in `src/`. For `testrunner`, focus on CLI flags, output formatting, and integration points. For `abstractions`, focus on interface shape changes.
- Don't prefix the body with the kind (no "Added: ..." in an Added fragment).

### 7. Show the user what you did

After writing files, list:
1. The fragments created — `<project> / <Kind> — <body first ~80 chars>`.
2. Anything you skipped because a fragment already existed.
3. Anything you weren't sure about — wrong project? wrong kind? combine or split? Ask before they walk away.

## Examples

### Single-project change

Branch adds canonical formatting to the F# Pacman parser. One commit: `feat(pacman): implement canonical_format`.

`.changes/unreleased/pacman-Added-20260502-130158.yaml`:
```yaml
project: pacman
kind: Added
body: Add canonical_format support to the F# Pacman parser, producing the round-trip-stable output required by the canonical_format test suite.
time: 2026-05-02T13:01:58.911103-07:00
```

### Change spanning abstractions + both parsers

Branch adds a new `ParseIndented` method to `ICclParser` and implements it in C# and F#. Three fragments — one per project — each phrased for that audience:

```yaml
project: abstractions
kind: Added
body: Add ICclParser.ParseIndented for parsing indentation-significant CCL fragments.
```

```yaml
project: ccldotnet
kind: Added
body: Implement CclParser.ParseIndented for indentation-significant fragments.
```

```yaml
project: pacman
kind: Added
body: Implement parse_indented in the F# Pacman parser.
```

### testrunner CLI change

Branch adds a `--filter` flag to the test runner.

```yaml
project: testrunner
kind: Added
body: Add --filter to run only tests whose name matches the given substring.
```

## Edge cases

- **No `.changie.yaml`**: not in ccl-dotnet, or it has been removed. Stop and tell the user.
- **No commits vs. main**: nothing to document. Say so; don't fabricate.
- **A change that touches `test/` only**: usually skip. If the user insists, attach to whichever project the test exercises.
- **Solution-wide config changes**: skip unless user-visible. Err on the side of asking.
- **User disagrees with the project assignment**: trust them — they may know about a downstream consumer you can't see — and update the file.
