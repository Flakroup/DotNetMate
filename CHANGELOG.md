# Changelog

All notable changes to DotNetMate will be documented in this file.

## [Unreleased]

Fixed:
- Structured logging in GitLogService (use Serilog templates instead of string interpolation)
- Remove fake async from DirectoryCleaner.GetDirectorySize
- RepositoryInfo handles repos without main/master branch gracefully
- Use typed InvalidOperationException instead of untyped exception in RepositoryInfo

Changed:
- Test classes marked as sealed per project conventions
- Removed placeholder test from CommitTagInfoTests

Added:
- CHANGELOG.md for tracking changes across releases
- ReSharperService tests: OrderConfigAsync sorting verification (3 new tests)
- DirectoryCleaner tests: nested bin/obj, .vs cleanup, .git protection, .binlog, empty dirs (6 new tests)
- GitLogVisualizer tests: CommitTagInfo with real repo, RepositoryInfo integration (4 new tests)

## [0.1.0] - 2025-03-09

Added:
- mate clean: remove bin/obj/.vs/.tmp/TestResults directories
- mate removeEmpty: remove empty directories
- mate gitlog: aggregate git logs across repositories
- mate resharper clean: clean JetBrains SolutionCaches
- mate resharper config --sort: sort .DotSettings files by XAML key
- Serilog + Sentry error tracking
- CI/CD pipeline for NuGet.org publishing
- DotNetMateTool moved to src/ directory structure
- GitLab CI with submodule support (recursive init)
