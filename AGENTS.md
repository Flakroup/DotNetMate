# DotNetMate

.NET CLI tool (`mate`) for keeping developer workspaces clean and organized.

## Architecture

- **DotNetMateTool** - CLI entry point (`System.CommandLine`), packaged as `dotnet tool`
- **DotNetMate.Core** - core logic (DirectoryCleaner, ReSharperService, Serilog setup)
- **GitLogVisualizer** - git log aggregation (LibGit2Sharp, WakaTime)
- **FEx/** and **FEx.WakaTime/** - git submodules (shared libraries)

## Key Rules

- Target framework: `net10.0` - do not introduce older TFMs
- **FEx and FEx.WakaTime are git submodules** - changes there require separate commits in the submodule repo
- Build system: NUKE (`./build.ps1`), targets: Compile, Test, Pack, Publish
- CI: GitLab CI, publish to nuget.org
- Conventions live in `DevConfigs/` (.editorconfig, Directory.Build.props/targets) - do not duplicate settings outside of DevConfigs
- AOT compatible: `IsAotCompatible = true` for non-test projects

## FEx Reuse

- Before writing new utility/helper logic, check `FEx/FEx-API-Catalog.json` for existing APIs
- Use FEx classes and extensions instead of reinventing - avoid redundant code

## GitLab Integration

- Use **GitLab MCP** server for: merge requests, issues, pipelines, branches
- CI: GitLab CI (`.gitlab-ci.yml`) - stages: build, test, publish

## Testing

- xUnit v3, NSubstitute, Shouldly, coverlet
- BenchmarkDotNet for perf benchmarks (dry run in CI)
- Test projects: `DotNetMate.Core.Tests`, `GitLogVisualizer.Tests`, `DotNetMate.Benchmarks`

## Security

- `NuGetOrgApiKey` used for publishing to nuget.org - never expose in code or output

## Project structure

- `src/DotNetMateTool/` - CLI app (entry point: Program.cs, DI: DotNetMateContainer.cs)
- `src/DotNetMate.Core/` - core logic library (config, IO, JB, logging)
- `tests/DotNetMate.Core.Tests/` - xUnit tests
- `tests/DotNetMate.Benchmarks/` - BenchmarkDotNet benchmarks
- `GitLogVisualizer/` - git log visualization library
- `build/` - NUKE build (Build.cs inherits from FExBuild)
- `work/` - working files and notes
- `artifacts/` - build artifacts (gitignored)

## Build commands

- `pwsh build.ps1 Compile` - compile
- `pwsh build.ps1 Test` - run tests (also runs benchmarks)
- `pwsh build.ps1 Full` - Clean + Pack
- `pwsh build.ps1 Info` - build info (version, branch)
- `pwsh build.ps1 InstallLocal` - install the tool globally
- `pwsh build.ps1 StampChangelog` - replace [Unreleased] with the version (CI only)

## CI/CD

- Runner: self-hosted Windows shell runner (PowerShell 7.x)
- Do NOT use Docker images - the runner is a shell executor
- Stages: build -> test -> publish -> cleanup
- Publish is triggered ONLY on push to main/master
- Submodules: GIT_SUBMODULE_STRATEGY=recursive
- Nested submodules (DevConfigs inside FEx) require manual init in CI

## Submodules

- `FEx` - branch `develop` (NOT master!)
- `DevConfigs` - shared build configuration
- `FEx.WakaTime` - WakaTime integration

## Publishing

- NuGet package: DotNetMateTool (tool command: `mate`)
- Auto-publish from main -> nuget.org (requires NuGetOrgApiKey)
- CHANGELOG.md is embedded as PackageReleaseNotes

## Project-specific rules

- CHANGELOG: user-facing changes only (goes into NuGet PackageReleaseNotes)
- Roadmap is maintained outside the repository (not in version control)
- Hierarchical .mate.json config - searched in the current directory and upward to home
