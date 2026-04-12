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

test:
    dotnet test

format:
    dotnet format

lint:
    dotnet format --verify-no-changes

clean:
    dotnet clean
    rm -rf src/*/bin src/*/obj test/*/bin test/*/obj

# Restore dependencies
deps:
    dotnet restore

ci: format lint test
alias pr := ci

# Run tests with verbose output
test-verbose:
    dotnet test --verbosity normal
