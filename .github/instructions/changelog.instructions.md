---
applyTo: ".changes/**,.changie.yaml"
description: How to create changie changelog fragments for ccl-dotnet (multi-project setup with ccldotnet, pacman, abstractions, testrunner).
---

# ccl-dotnet Changelog Fragments

When asked to create a changelog entry, change fragment, changie fragment, or otherwise document what changed on the current branch in this repo, follow the canonical guide at **[`docs/changelog-guide.md`](../../docs/changelog-guide.md)**.

Read that file before writing any fragment under `.changes/unreleased/`. It defines:

- the four projects (`ccldotnet`, `pacman`, `abstractions`, `testrunner`) and which paths map to which project
- the available `kind` values (Added, Fixed, Performance, Changed, Reverted, Dependencies, Security — there is no Breaking kind)
- the exact YAML field order and single-line `body` style used by existing fragments in `.changes/unreleased/`
- the filename pattern `<project-key>-<Kind>-<YYYYMMDD>-<HHMMSS>.yaml`
- when to write one fragment vs. several across projects, and what to skip (chore/docs/test commits, solution-wide config)

The same guide is used by the Claude Code skill at `.claude/skills/ccl-dotnet-changelog/SKILL.md`, so changes to the rules belong in `docs/changelog-guide.md`, not in this file.
