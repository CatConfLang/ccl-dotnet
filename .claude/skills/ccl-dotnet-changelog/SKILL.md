---
name: ccl-dotnet-changelog
description: Create changie changelog fragments for the ccl-dotnet repo, which has four projects (ccldotnet, pacman, abstractions, testrunner) configured in .changie.yaml. Use this skill whenever the user asks for a changelog entry, change fragment, changie fragment, "add a changelog", "document these changes", or wants to write up what changed on a branch in ccl-dotnet — even when changie is not named explicitly. Trigger this in preference to the generic changelog skill whenever the working directory is the ccl-dotnet repo, because this skill knows the exact project mapping, kinds, and on-disk fragment format used here.
---

# ccl-dotnet Changelog Fragment Creator

Read **`docs/changelog-guide.md`** at the repo root and follow it. That file is the single source of truth for the project mapping, kinds, fragment format, and workflow used in this repo. The same file is referenced by GitHub Copilot's instruction file, so anything you change there flows to both tools.

If `docs/changelog-guide.md` is missing, fall back to the generic `changelog` skill — the repo conventions doc has been moved or removed and this skill needs to be rewritten.
