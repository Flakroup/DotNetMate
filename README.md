# DotNetMate

A .NET developer CLI tool for keeping your workspace clean and organized.

## Installation

```bash
dotnet tool install -g DotNetMateTool
```

Requires .NET 10 runtime.

## Commands

### `mate clean`

Recursively removes build artifacts and temporary directories:

- `bin/`, `obj/`, `.vs/`, `.tmp/`, `TestResults/`
- `*Installer-cache/` directories
- `.nuke/temp/`
- `.binlog` files
- `*_wpftmp.csproj` files

```bash
mate clean                    # clean current directory
mate clean --folder C:\repos  # clean specific directory
```

### `mate removeEmpty`

Recursively removes empty directories (including those containing only system files like `desktop.ini`, `.DS_Store`, `Thumbs.db`).

```bash
mate removeEmpty                    # current directory
mate removeEmpty --folder C:\repos  # specific directory
```

### `mate gitlog`

Aggregates git commit logs across multiple repositories. Useful for timesheets and activity reports.

```bash
mate gitlog --from 2025-01-01                          # scan current directory
mate gitlog --root C:\repos --from 2025-01-01          # scan specific root
mate gitlog --from 2025-01-01 --exclude repo1,repo2   # exclude repos
mate gitlog --from 2025-01-01 --json                   # export to JSON
mate gitlog --from 2025-01-01 --csv                    # export to CSV
mate gitlog --from 2025-01-01 --with previous.json     # merge with existing data
```

### `mate resharper clean`

Cleans ReSharper/Rider temporary caches.

```bash
mate resharper clean
```

### `mate resharper config`

Manages ReSharper `.DotSettings` files.

```bash
mate resharper config --sort MySettings.DotSettings  # sort settings alphabetically
```

## License

[MIT](LICENSE.md)
