# Changelog

All notable changes to DotNetMate will be documented in this file.

## [0.1.8] - 2026-03-26

Performance:
- `mate clean` directory deletion significantly faster for large projects - uses native OS recursive delete as fast path, falls back to per-file enumeration only when locked files are encountered

## [0.1.7] - 2026-03-24

Added:
- Background update check: on each run, checks NuGet for a newer version and prints a notification after command output

Fixed:
- `mate clean` no longer reports errors for locked files - logs a warning instead of throwing (fixes DOTNETMATE-2, DOTNETMATE-3)
- Banner displaying wrong version (1.0.0) instead of installed version
- `gitlog` timezone normalization and duration calculation

## [0.1.6] - 2026-03-20

Added:
- Hierarchical `.mate.json` configuration file - directory traversal lookup (closest file wins, `~/.mate.json` as global fallback)
- Configurable defaults for `clean` (exclude patterns, custom directories), `gitLog` (default `--from`, `--tempo`), and `resharper` (DotSettings paths)
- `mate completions powershell|bash|zsh` command for shell completion scripts
- `mate clean` supports exclude patterns and custom directories via `.mate.json`

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
