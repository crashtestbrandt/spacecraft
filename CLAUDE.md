# Spacecraft — Claude Code context

## Writing for humans

Everything written for a human reader — PR titles, commit messages, issue and review comments, release notes,
in-game and UI copy, chat replies, this file and every page under `docs/` — is **concise, bulleted and
aphorism-free**. State plainly what changed and what it now does.

- No metaphor, euphemism, or oblique stand-in for the thing you mean.
- No epigrams or rhetorical inversions ("A *X* is not a *Y*", "not *X*, but *Y*").
- No teaser clause joined by a colon to the real content.
- No emphatic capitalization, and no general truth standing in for the specific change.
- Short sections, bullets over paragraphs, tables for enumerations, identifiers in backticks. Record the rule and
  its consequence; skip the narrative of how it was discovered. Bold the term a rule is about.

**A PR title is a release-notes line** — GitHub's generated notes quote it verbatim. Form: `<type>(<system>)
<plain description> (#issue)`, type one of `feat`, `fix`, `perf`, `refactor`, `docs`, `test`, `build`, `chore`.
Example: `feat(controls) the ship yaws with the mouse instead of A/D (#12)`.

## Project

Spacecraft is a 2D game built on **Godot 4.7, .NET (C#)** — the language choice is a deliberate divergence from
this author's other Godot projects, which are GDScript-only; nothing here should assume GDScript conventions.
Windows-only for now: the `justfile` shell is pinned to `powershell.exe`, and tooling is not expected to run on
Linux or macOS.

## Godot editor and export templates

**The Godot editor is a pinned download, not a committed file.** `godot.lock` names the exact
`godotengine/godot-builds` release tag and the SHA512 of every asset it pins, copied from that release's own
`SHA512-SUMS.txt`. `just godot-fetch` downloads, verifies and unpacks the mono/.NET win64 editor into
`.godot-editor/`, which is gitignored and absent from a fresh clone — nothing else installs it, and every recipe
that launches Godot refuses in one line until it's there (`godot-guard` in the `justfile`).

- `just godot-fetch -Templates` also installs the matching export templates, into whatever directory the
  fetched (or overridden) editor's own `--version` output names under `%APPDATA%\Godot\export_templates\` —
  computed from the binary, never guessed from the lock's tag, because a `.NET` build's version string carries
  its own `.mono` marker.
- `$env:GODOT_BIN` overrides the editor entirely: point it at an existing Godot 4.7 mono install to skip the
  fetch, exactly like the `GODOT_BIN` convention on this author's other Godot projects.
- `$env:SPACECRAFT_GODOT_HOME` moves the fetch's install location out of `.godot-editor/`, for a shared cache
  across more than one local project.
- Bumping the pinned version means bumping `godot.lock`'s `tag` and every digest in it, sourced from that
  release's `SHA512-SUMS.txt` — never hand-computed locally.

## Layout

`scenes/` (`.tscn`), `scripts/` (`.cs`), `project.godot`, `Spacecraft.csproj`, `tools/` (PowerShell scripts the
`justfile` wraps).
