# CCL-Dotnet Development Tasks

# Aliases
alias b := build
alias t := test
alias f := format
alias l := lint
alias c := clean

# Default task - show available commands
default:
    @just --list

# Required recipes
build:
    dotnet build

# Run the CCL test suite via the pluggable Spectre.Console runner.
# Extra args pass through to the runner (e.g. `just test --validation parse`).
test *ARGS:
    dotnet run --project test/CclDotnet.TestHost -- {{ARGS}}

# Same as `test` but with verbose per-test output
test-verbose *ARGS:
    dotnet run --project test/CclDotnet.TestHost -- --verbose {{ARGS}}

# Run only one validation type (parse, build_hierarchy, get_string, …)
test-validation NAME *ARGS:
    dotnet run --project test/CclDotnet.TestHost -- --validation {{NAME}} {{ARGS}}

format:
    dotnet format

lint:
    dotnet format --verify-no-changes

clean:
    dotnet clean
    rm -rf src/*/bin src/*/obj test/*/bin test/*/obj tools/*/bin tools/*/obj

# Restore dependencies
deps:
    dotnet restore

ci: format lint build test
alias pr := ci

# Create a new changelog entry for a project (abstractions | testrunner)
change PROJECT:
    changie new --projects {{PROJECT}}

# Preview the next version changelog for a project (abstractions | testrunner)
changelog-preview PROJECT:
    changie batch auto --project {{PROJECT}} --dry-run
