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
dotnet_check_script := justfile_directory() / "tools" / "dotnet-check.ps1"
dotnet_fetch_script := justfile_directory() / "tools" / "dotnet-fetch.ps1"
demo_script := justfile_directory() / "tools" / "demo.ps1"
demo_test_script := justfile_directory() / "tools" / "demo-test.ps1"

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

# Verify a .NET SDK >= 8 is on PATH (a runtime alone can't build). One readable line if not.
dotnet-check:
    & "{{dotnet_check_script}}"

# Install the .NET 8 SDK via winget unless dotnet-check already passes. Machine-wide, not a pinned
# local download like godot-fetch: Godot's Build button only finds the `dotnet` on PATH. -Force
# reinstalls. NuGet sources need no setup -- nuget.config in the repo root declares nuget.org.
dotnet-fetch *ARGS:
    & "{{dotnet_fetch_script}}" {{ARGS}}

# Compile the C# assembly (the editor's Build button does the same). Guarded by dotnet-check.
build: dotnet-check
    dotnet build -nologo

# Rebuild .godot/ -- needed after a checkout the editor hasn't opened yet, or after godot-fetch
# installs a different editor build.
reimport:
    & "{{godot}}" --headless --path . --import

# Local multiplayer demo: one host window + (N-1) guest windows on 127.0.0.1:7777. Click Ready in
# each. `just demo 4` for four players; `just demo 2 -AutoPlay` readies every instance and scripts a
# full-army charge so the match plays itself. See docs/session.md.
demo N="2" *ARGS:
    & "{{demo_script}}" -Players {{N}} {{ARGS}}

# Headless self-test over the real ENet loopback: N processes host/join, auto-ready, verify their
# armies on the verified timeline and exit 0. Nonzero exit if any peer fails. `just demo-test 4`,
# add -AutoPlay to also run the scripted engagement.
demo-test N="2" *ARGS:
    & "{{demo_test_script}}" -Players {{N}} {{ARGS}}
