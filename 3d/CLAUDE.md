# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

LionQuest **3D** — top-down 3D action game. Unity **6000.3.2f1** (Unity 6). This directory (`3d/`) is one of two Unity projects under the `LionQuest/` repo root (the sibling `2d/` is a separate project). Rules in `LionQuest/.cursorrules` apply to both — the important parts are folded into this file.

Players earn **souls** by killing enemies, spend them to upgrade stats. Each character class has a unique ID; stats persist across sessions via JSON save.

## Build & Run

- Open the project: Unity Hub → open `3d/` with editor `6000.3.2f1`.
- Play: press Play in the editor.
- Tests: Unity Test Runner (`Window → General → Test Runner`) — `com.unity.test-framework` is installed, but no test assemblies exist yet.
- Code generators: run from the editor menu `LionQuest/Generate/...` (editor scripts in `Assets/Editor/`).

## Agent Automation

Three lanes for agent-driven work. Choose by task type:

| Task | Lane |
|---|---|
| Scalar/data tuning in existing `.asset` (e.g. `damage:`, `cooldown:`) | **Direct YAML edit** — no Unity launch |
| Structural: new scene/prefab/SO, ref wiring, run a generator | **Batch CLI headless** |
| Fast interactive iteration with editor open | **MCP bridge** (Tools > Agent Bridge/Start) |

### Lane 1 — Batch CLI headless

```sh
# Compile gate (run after any script edit)
sh tools/unity-batch.sh Compile

# Run a named generator
sh tools/unity-batch.sh RunGenerator StartScreen
sh tools/unity-batch.sh RunGenerator CharacterSelection
sh tools/unity-batch.sh RunGenerator UpgradeScreen

# Run any menu item by path
sh tools/unity-batch.sh RunGeneratorByMenuPath "LionQuest/Generate/Start Screen"

# Print editor facts not visible on disk (serialization mode, scene loadability)
sh tools/unity-batch.sh Diagnose
```

Batch mode needs the sandbox disabled — Unity writes to `Library/`, editor prefs, and a licensing mutex outside sandbox-allowed paths. Expected, not a bug.

**Sentinel lines** grep'd to stdout: `AGENT_BATCH_OK|...` (success) or `AGENT_BATCH_FAIL|...` (failure). Compile errors surface as `): error CS####`. Exit code 0 = OK, 1 = failure.

**Lock caveat**: batch fails if the Unity editor is open (`Temp/UnityLockfile` exists). The script detects this and prints a clear error. Close editor first, or use the MCP bridge.

### Lane 2 — MCP bridge (interactive)

With the editor open: `Tools > Agent Bridge > Start` → listens on `127.0.0.1:7891`.

Commands (newline-terminated, via `nc` or `socat`):
```
ping                            → pong
compile                         → OK|compile refresh triggered
menu LionQuest/Generate/...     → OK|menu executed
play                            → OK|entering play mode
stop                            → OK|stopping play mode
```

Example: `echo "menu LionQuest/Generate/Start Screen" | nc 127.0.0.1 7891`

### Known constraint — `SampleScene.unity` is binary

`EditorSettings.serializationMode` is `ForceText` and all prefabs/`.asset` files are YAML, but `Assets/Scenes/SampleScene.unity` predates that setting and is still **binary**. Agents cannot read or diff it, and git treats it as an opaque blob (no reviewable diffs, unresolvable merge conflicts). Neither `EditorSceneManager.SaveScene` nor `AssetDatabase.ForceReserializeAssets` converts it — both write back in the file's existing format, even when saving to a fresh path. Converting it likely needs the GUI (`Edit > Project Settings > Editor`, re-apply Force Text, then open+save the scene in the editor).

Until it is converted: scene changes to `SampleScene` must go through a generator that edits it via the Unity API (`EditorSceneManager`) — never by text manipulation. Expect its diffs to be unreviewable.

### Lane 3 — Direct YAML data edits

Agents may edit **scalar values** in existing `.asset`/`.prefab` YAML directly (single numbers/bools/strings). Do NOT hand-edit structural YAML (adding components, wiring `{fileID, guid, type}` refs, creating objects) — use a generator (Lane 1). After a scalar edit, run `sh tools/unity-batch.sh Compile` if the file could affect import; pure data scalars need no relaunch.

### Generator convention

Every generator in `Assets/Editor/` exposes **both** a `[MenuItem("LionQuest/Generate/...")]` (for human menu click) and a `public static void Generate()` (for `AgentBatch.RunGenerator` to call directly). This dual-entry pattern is required for all generators.

## Key Packages

Universal Render Pipeline (URP 17.3), new **Input System 1.17.0** (not legacy `Input`), uGUI 2.0 + TextMeshPro, AI Navigation. UI is **uGUI (Canvas)**, not UI Toolkit — see `doc/startScreen.md` for rationale.

## Architecture

Scripts live flat in `Assets/Scripts/` (**no namespaces** — global namespace throughout). Deep dive with concrete formulas and call chains is in `doc/project.md`; read it before touching combat, saves, or progression.

### Core systems

- **Save/progression**: `SaveManager` (static JSON read/write, per-character slots at `Application.persistentDataPath/gamesave.json`) → `SaveDataTypes` POCOs (`GameSaveData`, `CharacterSaveData`) → `CharacterStats` (per-player stat levels, soul currency, getters, upgrade methods).
- **Global manager**: `GameSaveController` — singleton (`Instance`), tracks active players, fires soul rewards on enemy kills, persists immediately.
- **Combat**: input in `TopDownPlayerController` → `CombatController.PerformAttack(attackData)` → instantiates `AttackEffect` (melee, collider-based hit detection) or `Projectile` (skill 2). Damage/attack params come from `AttackData` / `ProjectileSkillData` ScriptableObjects. Final damage = base × `CombatController.damageMultiplier` (from `CharacterStats.GetDamageMultiplier()`).
- **Enemy**: `Enemy` holds health + `TakeDamage`/`Die`; death fires `OnDeath` → `GameSaveController.OnEnemyKilled()` → soul reward. `EnemyAnimator`, `LootDropper`/`LootTable` for drops.

### Planned features (design docs, not yet implemented)

`doc/startScreen.md` and `doc/characterSelection.md` — full implementation plans for a start screen and 4-player local character-select flow. Both use the code-generated workflow (runtime scripts + one editor generator per feature, menu `LionQuest/Generate/...`). Known orphaned code (`speedLevel`, `unlockedSkills`, upgrade UI) is catalogued in `doc/project.md`.

## Project Conventions (from `.cursorrules`)

- **No lookup fallbacks**: never use `FindObjectOfType`, `FindGameObjectWithTag`, `Camera.main` to locate scene objects. Required refs are assigned in the Inspector; if missing, `Debug.LogError` and fail loudly — do not silently fall back.
- **Movement via physics**: all movement uses `Rigidbody.AddForce()`. Never set `Rigidbody.linearVelocity` or `transform.position` directly for movement. Configure mass/drag (`linearDamping`)/constraints on the Rigidbody in the Inspector, not as script fields.
- **Fewer, fatter scripts**: prefer adding functionality to an existing script over creating a new one. Health belongs in `Enemy`, not a separate `Health` script. Don't duplicate fields that already live on components (mass/drag → Rigidbody, not script).
- **SetupHelper pattern**: one `*SetupHelper` editor script per entity type (`PlayerSetupHelper`, `EnemySetupHelper`) adds required components via `[ContextMenu]`. Editor-only setup, not runtime logic.
- **Scene files (`.unity`) may be created/modified by AI agents**, preferably via editor generator scripts (code-generated scenes) per the agentic workflow.
- **Do NOT create `.md` files** unless explicitly requested.

## Git

Do not stage or commit unless asked (leave changes in the working tree for review). Meta files (`*.meta`) are tracked; `Library/`, `Temp/`, `Logs/`, `UserSettings/` are ignored.
