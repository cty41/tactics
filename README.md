# Tactics

> [English](README.md) · [中文](README.zh-CN.md)


Tactics is a turn-based tactical Roguelike built with Godot 4.7 C#. The current core mode, **Pure Run**, centers on a fixed three-character party of Mage, Necromancer, and Amazon: players pick a path through seven floors of battle, event, and service nodes, combine skills, attributes, gear, and consumables within a single run, and bring the party to the final boss.

> Project status: in-development prototype. The main loop and automated regression are established, but visuals, feel, and the release experience still require continuous manual acceptance; the current build does not represent a final release.

## Open-source license

- Source code, documentation, and repository tooling are released under the [Apache License 2.0](LICENSE).
- `godot/assets/` and project-owned images explicitly listed in the provenance manifest are released under [CC BY 4.0](ASSET_LICENSE.md), attributed to `cty41`.
- The project name, logos, and release branding are not granted as trademarks by the code or asset licenses; see [TRADEMARKS.md](TRADEMARKS.md).
- Third-party components and their licenses are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Per-file asset provenance and hashes are recorded in `Tools/public-release/asset-provenance.json`.

The license statements cover only content explicitly included in the repository's public provenance manifest. They do not cover Unity projects, candidate art, reverse-engineering material, or third-party reference payloads kept in private historical archives.

## Core content

- Deterministic grid combat, turn order, skill effects, statuses, and enemy AI.
- A seven-floor Pure Run map with normal battle, Elite, Rest, Store, Mystery, Treasure, and Boss nodes.
- Three classes with Lv1–Lv3 skill growth, attribute allocation, gear, consumables, and persistent buffs.
- Save V6 checkpoints, crash recovery, and repeatably verifiable run state.
- Godot-native UI, isometric board, procedural battle presentation, and the Content Workbench.
- A regression chain of NUnit, Frozen Oracle, GdUnit, Gameplay Specs, and dual-renderer smoke.

## Technical architecture

```text
src/Tactics.Core
    Pure .NET 9 gameplay rules, deterministic RNG, Battle and Run state
            ↓
src/Tactics.Application
    Use cases, content compilation, save and runtime projection
            ↓
godot/src/Tactics.Godot.Adapter
    Godot Nodes, Resources, Scenes, UI, Input and Presentation
            ↓
godot/project.godot + godot/content + godot/scenes
```

`Tactics.Core` and `Tactics.Application` do not depend on Godot. Engine objects, resource loading, filesystem work, and UI logic are concentrated in the Adapter layer; runtime content is driven by Godot Resources, PackedScenes, and Catalogs.

## Quick start

### Requirements

- Windows
- [Git LFS](https://git-lfs.com/)
- Godot `4.7.1-stable` Mono/.NET build
- .NET SDK `9.0.312`, or a compatible newer patch within the same feature band

### Get and run

```powershell
git clone git@github.com:cty41/wooftactics.git
Set-Location wooftactics
git lfs pull --include="godot/**" --exclude=""
```

Import and open in the Godot Project Manager:

```text
godot/project.godot
```

The project's main scene is `godot/scenes/Main.tscn`. On first open, wait for Godot to finish resource import and C# compilation, then run from the editor.

## Verification

The repository's unified verification entry point runs, in sequence: locked restore, .NET build, NUnit, Frozen Oracle, Gameplay Specs, GdUnit, headless Runtime/Editor, catalog ownership, OKF, and rendering compatibility checks:

```powershell
pwsh -NoProfile -File .\Tools\godot\Verify-GodotProject.ps1 `
  -GodotExecutable "D:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe"
```

The full gate also requires the Python and Node.js/npm dependencies used by repository tooling. The script stops and reports the reason when required tools are missing or versions mismatch.

## LLM-assisted design and development

The project supports turning natural-language design requests into stable `gameplay-contract`s, then proposing contract and test candidates with source-quoted evidence through OpenCode Go or a local Ollama, gated deterministically by strict Schema, capability, compiler, typed-authoring, and Godot ResourceSaver checks. The external LLM is a replaceable, batchable, auditable candidate-generation layer — it does not replace Codex/developer judgment, and it must not write Resources, runtime code, or approve human acceptance directly.

For the full workflow from requirement convergence and provider configuration through Scenario/Enemy Draft, Godot authoring, automated tests, and manual acceptance, see [LLM-assisted design to Godot development](.agents/docs/gameplay-design-to-development-workflow.md).

## Windows builds

The **Godot Windows Debug and Release build** workflow on GitHub Actions lets you choose manually:

- `debug`: development/debug package using the Godot Debug export.
- `release`: a Release-candidate package close to the player-deliverable form.
- `both`: builds both flavors in sequence after one shared verification; the default option.

You can also build locally after installing the matching Godot Export Templates:

```powershell
pwsh -NoProfile -File .\Tools\godot\Build-GodotWindows.ps1 `
  -GodotExecutable "D:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe" `
  -BuildFlavor Both
```

Output goes to `Build/Godot/Windows-Debug` and `Build/Godot/Windows-Release`. The build package's automated smoke cannot replace manual launch and gameplay acceptance in a clean Windows environment.

## Repository map

| Path | Responsibility |
|---|---|
| `src/Tactics.Core` | Engine-agnostic gameplay rules and deterministic state |
| `src/Tactics.Application` | Application use cases, content conversion, and save boundaries |
| `godot/` | The single Godot project: Adapter, Scenes, Resources, and test host |
| `Tools/godot/` | Mainline verification, Windows builds, package audit, and launch smoke |
| `Tools/gameplay-test-spec/` | Gameplay contracts, LLM candidates, Gameplay Spec, and deterministic compilation tooling |
| `Tests/gameplay-specs/` | Platform-neutral Gameplay Specs and Godot execution plans |
| `.agents/docs/` | Current design, acceptance boundaries, and project constraints |
| `.agents/knowledge/` | OKF project knowledge index and cross-system navigation |
| `.agents/skills/` | Project-specific agent skills; generic skills come from the global shared repo (see "Shared Agent Skills") |

## Shared Agent Skills

Generic agent workflow skills (`grill-me`/`grilling`, `brainstorming`, `make-dev-plan`, `plan-mode-plan-writer`, `project-doc-organization`, `skill-writing`, and others) are installed user-globally at `~/.agents/skills` from the public [`cty41/skills`](https://github.com/cty41/skills) repository (`git clone git@github.com:cty41/skills.git`, then run `scripts/install-user.ps1` in that checkout; macOS/Linux run the same script under pwsh). They are not vendored here; update by running `git -C <skills-checkout> pull` and re-running the installer.

This repository's `.agents/skills/` keeps only project-specific skills (`godot-*`, `gameplay-*`, `artworks-prompt-library`, `pure-run-artwork-pipeline`) plus two intentional specializations — `knowledge-maintenance` (full `Tools/okf`) and `manual-qa-handoff` (hard-referenced by `Tools/agent-policy`). Project-local skills take precedence over the global install. See the "Shared agent skills" section in [AGENTS.md](AGENTS.md) for the full contract.

## Project history and collaboration

On `main`, Godot is the product and runtime authority. The legacy Unity project has been retired; the permanent tag `unity-final-2026-08-08`, Frozen Oracle, Golden, and migration receipts are used only for historical behavior and provenance audits, not for the current runtime.

Please read [AGENTS.md](AGENTS.md) before modifying the project. It defines the single Godot project, C# layering, Resource writing, Editor lifecycle, verification, and dirty-worktree protection rules.

Issues and pull requests are welcome. Development environment, verification requirements, asset contribution conditions, and commit boundaries are described in [CONTRIBUTING.md](CONTRIBUTING.md); report security issues privately per [SECURITY.md](SECURITY.md).