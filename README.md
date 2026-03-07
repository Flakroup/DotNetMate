# DotNetMate

A .NET developer CLI tool for keeping your workspace clean and organized.

## Installation

```bash
dotnet tool install -g DotNetMateTool
```

Requires .NET 10 runtime.

## Commands

### `mate clean`

Recursively scans and removes build artifacts, temporary directories, and generated files. Displays cleanup statistics with total size reclaimed.

**Directories removed:**

| Pattern | Description |
|---------|-------------|
| `bin/` | Build output |
| `obj/` | Intermediate build files |
| `.vs/` | Visual Studio local settings |
| `.tmp/` | Temporary directories |
| `TestResults/` | Test result output |
| `*Installer-cache/` | Installer cache directories |
| `.nuke/temp/` | NUKE build system temp files |

**Files removed:**

| Pattern | Description |
|---------|-------------|
| `*.binlog` | MSBuild binary logs |
| `*_wpftmp.csproj` | WPF temporary project files |

After deletion, empty directories left behind are automatically cleaned up.

```bash
mate clean                    # clean current directory
mate clean --folder C:\repos  # clean specific directory
```

### `mate removeEmpty`

Recursively removes empty directories. Directories containing only system/metadata files are treated as empty and removed along with those files.

**System files considered as empty content:**
`desktop.ini`, `.DS_Store`, `Thumbs.db`, `metadata.opf`, `cover.jpg`

`.git` directories are always protected and never deleted.

```bash
mate removeEmpty                    # current directory
mate removeEmpty --folder C:\repos  # specific directory
```

### `mate gitlog`

Aggregates git commit logs across multiple repositories into a single report. Useful for timesheets and activity reports.

```bash
mate gitlog --from 2025-01-01                          # scan current directory
mate gitlog --root C:\repos --from 2025-01-01          # scan specific root
mate gitlog --from 2025-01-01 --exclude repo1,repo2   # exclude repos
mate gitlog --from 2025-01-01 --json                   # export to JSON
mate gitlog --from 2025-01-01 --csv                    # export to CSV
mate gitlog --from 2025-01-01 --with previous.json     # merge with existing data
```

### `mate resharper clean`

Cleans ReSharper/Rider `SolutionCaches` directories under `%LOCALAPPDATA%\JetBrains`.

```bash
mate resharper clean
```

### `mate resharper config`

Sorts entries in ReSharper `.DotSettings` files alphabetically by XAML key.

```bash
mate resharper config --sort MySettings.DotSettings
```

## License

[MIT](LICENSE.md)
