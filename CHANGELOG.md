# Changelog

All notable changes to DotNetMate will be documented in this file.

## [0.1.5] - 2026-03-16

## [0.1.5] - 2026-03-16

Added:
- `--tempo` (`-t`) option in `gitlog` command - aggregates commits per day/branch with time range and estimated work time for Tempo/JIRA logging
- Recover commits from deleted/merged branches by walking merge commit second parents
- Extract original branch name from merge commit messages (e.g. "Merge branch 'feature/PM3-1305' into develop")

Fixed:
- `--tempo` sort order: oldest day first
- `--tempo` time calculation: use local time instead of UTC to avoid normalization across mixed offsets
- `--tempo` output alignment: consistent column formatting
- Branch attribution: feature branches now exclude base branch commits
- Branch attribution: prefer feature branch name over generic (main/master/develop) in duplicate resolution
- Filter out `origin/HEAD` symbolic ref from branch listing

## [0.1.4] - 2026-03-12

Fixed:
- `mate clean` performance: parallel folder size calculation
- `mate gitlog` handles repositories without main/master branch gracefully
- Improved console output formatting (structured logging)

## [0.1.3] - 2026-03-12

Fixed:
- Removed `sentry-test` CLI command (not intended for release)

## [0.1.2] - 2026-03-12

Fixed:
- Sentry error tracking configuration

## [0.1.1] - 2026-03-11

Fixed:
- Internal configuration fixes

## [0.1.0] - 2026-03-09

Added:
- `mate clean` - remove bin/obj/.vs/.tmp/TestResults directories
- `mate removeEmpty` - remove empty directories
- `mate gitlog` - aggregate git logs across repositories
- `mate resharper clean` - clean JetBrains SolutionCaches
- `mate resharper config --sort` - sort .DotSettings files by XAML key
- Sentry error tracking
