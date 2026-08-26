# This is a Windows-only project (see CLAUDE.md), so the shell is pinned straight to PowerShell --
# no bash/Git Bash dance, no `set windows-shell` split. Every recipe body below is a PowerShell
# snippet.
set shell := ["powershell.exe", "-NoProfile", "-Command"]

# tools/godot-run.ps1 resolves the editor (GODOT_BIN, else whatever `just godot-fetch` installed
# into .godot-editor/) and refuses with one readable line if neither is there yet -- every recipe
# below runs through it rather than naming a binary path directly. `just` variables can't run a
# backtick command through `{{...}}` interpolation, so this has to be a static script path (same
# shape as spaceman's own `godot := justfile_directory() / "tools" / "godot-quiet.sh"`), not a
# resolved binary path.
godot := justfile_directory() / "tools" / "godot-run.ps1"
godot_fetch_script := justfile_directory() / "tools" / "godot-fetch.ps1"

default:
    @just --list

# Install the pinned Godot 4.7 .NET editor (see godot.lock) into .godot-editor/, gitignored. Add
# -Templates to also fetch the matching export templates, or -Force to re-fetch either even when
# already installed. Point $env:GODOT_BIN at an existing Godot 4.7 mono install to skip this
# entirely -- every recipe below resolves through it first. See README.md.
godot-fetch *ARGS:
    & "{{godot_fetch_script}}" {{ARGS}}

editor:
    & "{{godot}}" --editor --path .

run:
    & "{{godot}}" --path .

# Rebuild .godot/ -- needed after a checkout the editor hasn't opened yet, or after godot-fetch
# installs a different editor build.
reimport:
    & "{{godot}}" --headless --path . --import
