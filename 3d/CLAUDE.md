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

## Lessons learned (headless batch + terrain generation)

- **Zombie `Unity.Licensing.Client` process blocks all future headless runs.** Killing a batch run mid-flight (Ctrl-C, timeout, `kill`) can leave the child `Unity.Licensing.Client` process alive holding the global named-pipe mutex (`Unity-LicenseClient-<user>`). Every subsequent `-batchmode` launch then times out waiting 60s for that channel and never reaches `-executeMethod` — even though `Temp/UnityLockfile` looks free and no `Unity` editor process is running. Symptom in `Logs/agent-batch.log`: repeated `Failed to acquire global mutex Unity-LicenseClient-atte. Another instance of Unity.Licensing.Client is already running.` Fix: `pgrep -fl "Unity.Licensing.Client"` and kill any hit before retrying. Check this whenever a batch run hangs or times out, not just when it fails outright.
- **`tools/unity-batch.sh` exit code alone is not proof of success** — it can exit 0 without ever reaching `-executeMethod` (licensing failure leaves module assemblies unloaded). Always require the `AGENT_BATCH_OK` sentinel in the log, never trust `$?` alone. (The script now enforces this itself — see its tail.)
- **A visual/gameplay bug can come from stale Inspector-serialized values, not the code.** After rewriting `MapGenerator.cs` correctly (per the design doc), the scene still rendered wrong (two flat water blocks, near-zero grass) because the scene's serialized `MapGenerator` component had leftover bad values from an earlier iteration: `flatTerrain=true`, `terrainHeight=1` (so every height clamped to max), `mapSize=(50,50)` with oversized lake radii. The C# was fine; the `.unity` scene's field values were not. **Always dump the actual serialized values from the scene headlessly before assuming a code fix will show up** — don't rely on defaults in the script or on memory of what you last set. Pattern: add a throwaway `AgentBatch.Diagnose*` method that opens the scene, `new SerializedObject(component)`, and `Debug.Log`s each relevant `SerializedProperty` (see `AgentBatch.DiagnoseMapGenerator`/`PropVal` for the template — switch on `SerializedPropertyType`).
- **A content/wiring generator should force-correct critical scalar fields it depends on, not just wire references.** `TerrainContentGenerator.WireSceneMapGenerator()` now overwrites `mapSize`/`terrainHeight`/`flatTerrain`/`islandRadius`/`oceanDepth`/`beachWidth` on every run, instead of only assigning materials/textures and trusting whatever the Inspector already had. Otherwise every regeneration inherits whatever bad state a prior manual edit or earlier generator version left behind.
- **You can run arbitrary runtime logic headlessly, not just menu-item generators.** Added `AgentBatch.RunGenerator GenerateMap` which opens `SampleScene`, finds the `MapGenerator` component, and calls `generator.GenerateMap()` directly (not via `[MenuItem]`), then saves the scene. This is how Phase 2/3 procedural logic (lake carving, splines, alphamap blending) got verified for actual runtime exceptions headlessly, versus only checking that it compiles. Prefer this over trusting "compiles clean" as proof that generation logic works.
- **URP terrain shaders render at most 4 layers per pass.** A 5th needs a separate add-pass shader with its own `Dependency` declaration (unverified how well that behaves). Given 4 slots (Grass/Dirt/Hole/Sand), water was made a runtime-spawned `Quad` mesh with its own hand-written shader instead of a 5th terrain layer.
- **URP terrain/lit shaders ship with `Fallback "Hidden/Universal Render Pipeline/FallbackError"`**, which silently renders pink on any shader compile failure with no console error. Deliberately omit `Fallback` from hand-written shaders during development so failures surface as real compile errors instead of a silent visual bug.
- **`Destroy()` throws outside Play mode.** Any method reachable from `[ContextMenu]` (edit-mode callable) that destroys objects needs an edit-mode-safe branch: `Application.isPlaying ? Destroy(x) : DestroyImmediate(x)`. See `MapGenerator.DestroySafely`.
- **Runtime-spawned objects (e.g. water plane) survive domain reload as scene children but lose their C# list reference.** Don't rely solely on a tracked `List<Transform>` to find/replace them on regeneration — sweep by a name prefix (`WaterSurfacePrefix = "WaterSurface_"`) among the parent's children first, since the list is empty after reload but the GameObject is still there.
