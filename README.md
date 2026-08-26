# Spacecraft

A 2D Godot 4.7 project, .NET (C#). Windows-only for now.

## Install `just`

Recipes in this repo run through [`just`](https://github.com/casey/just), a command runner. Pick
one:

```powershell
winget install --id Casey.Just -e   # built into Windows 10 21H2+ / Windows 11
scoop install just                  # if you use Scoop
choco install just                  # if you use Chocolatey
```

Verify with `just --version`. No other install is required: this repo's `justfile` pins its shell
straight to `powershell.exe`, which ships with every Windows box, so recipe bodies never need
Git Bash or WSL.

## Quick start

```powershell
just                # list every recipe
just godot-fetch     # install the pinned Godot 4.7 .NET editor into .godot-editor/ (gitignored)
just editor          # open the project
just run             # run the main scene
```

`just godot-fetch` is not optional on a fresh clone: the Godot editor is a pinned download rather
than a committed file (see `godot.lock`), so nothing else installs it, and every recipe that
launches Godot refuses with one line until it's there. Add `-Templates` when you need to export a
build (`just godot-fetch -Templates`).

Already have a Godot 4.7 .NET editor installed? Set `$env:GODOT_BIN` to its `.exe` path and skip
the fetch — every recipe resolves the binary through that variable first. To keep a fetched editor
somewhere other than `.godot-editor/` (a cache shared across projects, say), set
`$env:SPACECRAFT_GODOT_HOME` to that directory before running `just godot-fetch`.

## Conventions

See [CLAUDE.md](CLAUDE.md).
