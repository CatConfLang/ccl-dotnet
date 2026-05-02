# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## v0.2.0 - 2026-05-02


### Added

- Dispatch the `build_model` validation type. Tests with `function: build_model` now invoke `ICclParser.BuildModel` and assert against the expected object shape, mirroring `build_hierarchy` handling.

## v0.1.0 - 2026-05-02


### Added

- Initial release. Pluggable Spectre.Console-based runner that loads the shared ccl-test-data corpus, classifies tests against the implementation's config, executes them, and emits human-readable plus machine-readable (test-results-format v1.1.0) output.

