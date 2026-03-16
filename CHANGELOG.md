# Changelog

All notable changes to DotNetMate will be documented in this file.

## [Unreleased]

Added:
- `--tempo` (`-t`) option in `gitlog` command - aggregates commits per day/branch with time range and estimated work time for Tempo/JIRA logging
- Recover commits from deleted/merged branches by walking merge commit second parents on the base branch
- Extract original branch name from merge commit messages (e.g. "Merge branch 'feature/PM3-1305' into develop")
- PackageReleaseNotes in DotNetMateTool.csproj - CHANGELOG.md embedded in NuGet package
- RepositoryInfo integration tests (RepositoryInfoTests)

Fixed:
- `--tempo` sort order: oldest day first
- `--tempo` time calculation: use `When.DateTime` instead of `DateTimeOffset` to avoid UTC normalization across mixed offsets
- `--tempo` output alignment: use PadRight instead of tabs for column formatting
- Branch attribution: feature branches now exclude base branch commits (ExcludeReachableFrom)
- Branch attribution: prefer feature branch over generic (main/master/develop) in DistinctBy
- Filter out `origin/HEAD` symbolic ref from branch listing
- Structured logging in GitLogService (Serilog templates instead of string interpolation)

Changed:
- CI switched from Docker image to Shell executor on JOHNNYABQPC
- CI submodule init: explicit per-submodule auth with CI_JOB_TOKEN (including nested DevConfigs)
- Build: use `ITestTarget` from FEx.Building instead of local `Test` target

## [0.1.4] - 2026-03-12

Fixed:
- Structured logging in GitLogService (use Serilog templates instead of string interpolation)
- DirectoryCleaner.GetDirectorySize now truly async with parallel folder size calculation
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

## [0.1.3] - 2026-03-12

Fixed:
- Remove unnecessary sentry-test CLI command

## [0.1.2] - 2026-03-12

Fixed:
- Embed Sentry DSN directly in SerilogConfiguration

## [0.1.1] - 2026-03-11

Fixed:
- CI Tag target to use lightweight tags
- Add sentry-test command for development

## [0.1.0] - 2026-03-09

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
