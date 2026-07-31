# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

LionQuest **3D** — top-down 3D action game. Unity **6000.3.2f1** (Unity 6). This directory (`3d/`) is one of two Unity projects under the `LionQuest/` repo root (the sibling `2d/` is a separate project). Rules in `LionQuest/.cursorrules` apply to both — the important parts are folded into this file.

Players earn **souls** by killing enemies, spend them to upgrade stats. Each character class has a unique ID; stats persist across sessions via JSON save.

## Build & Run

- Tests: `sh tools/unity-test.sh <Filter>` (headless PlayMode; assemblies in `Assets/Tests/PlayMode/`).
- Code generators: run from the editor menu `LionQuest/Generate/...` (editor scripts in `Assets/Editor/`).

## Agent Automation

Three lanes for agent-driven work. Choose by task type:

| Task | Lane |
|---|---|
| Scalar/data tuning in existing `.asset` (e.g. `damage:`, `cooldown:`) | **Direct YAML edit** — no Unity launch |
| Structural: new scene/prefab/SO, ref wiring, run a generator | **Batch CLI headless** |
| Fast interactive iteration with editor open | **MCP bridge** (Tools > Agent Bridge/Start) |

Commands for lanes 1 and 2 (`tools/unity-batch.sh`, the Agent Bridge socket protocol) and the
failure modes of headless runs are in the **`unity-headless` skill** — invoke it before running
anything headlessly. Two non-negotiables from it: **never trust `tools/unity-batch.sh`'s exit code
as proof of success** — require the `AGENT_BATCH_OK` sentinel in the log; and a batch run that
hangs is usually a zombie `Unity.Licensing.Client` process, not your code.

### Known constraint — `SampleScene.unity` is binary

`EditorSettings.serializationMode` is `ForceText` and all prefabs/`.asset` files are YAML, but `Assets/Scenes/SampleScene.unity` predates that setting and is still **binary**. Agents cannot read or diff it, and git treats it as an opaque blob (no reviewable diffs, unresolvable merge conflicts). Neither `EditorSceneManager.SaveScene` nor `AssetDatabase.ForceReserializeAssets` converts it — both write back in the file's existing format, even when saving to a fresh path. Converting it likely needs the GUI (`Edit > Project Settings > Editor`, re-apply Force Text, then open+save the scene in the editor).

Until it is converted: scene changes to `SampleScene` must go through a generator that edits it via the Unity API (`EditorSceneManager`) — never by text manipulation. Expect its diffs to be unreviewable.

### Lane 3 — Direct YAML data edits

Agents may edit **scalar values** in existing `.asset`/`.prefab` YAML directly (single numbers/bools/strings). Do NOT hand-edit structural YAML (adding components, wiring `{fileID, guid, type}` refs, creating objects) — use a generator (Lane 1). After a scalar edit, run `sh tools/unity-batch.sh Compile` if the file could affect import; pure data scalars need no relaunch.

### Verification screenshots

Every screenshot taken to verify or prove an implementation works goes in **`3d/screenshots/`**
(gitignored). `HarnessFixture.Shot(name)` writes `screenshots/test-<name>.png`;
`AgentBatch.CaptureToPng` writes `screenshots/<file>.png`. New capture code must reuse one of
those two helpers instead of choosing its own directory.

Do **not** write screenshots to `Logs/` (wiped and rewritten by each run) or to `Temp/`/`temp/`
— those are the same directory on this case-insensitive filesystem, Unity deletes it when the
editor quits, and it is gitignored, so the proof disappears before it can be read back.

### Generator convention

Every generator in `Assets/Editor/` exposes **both** a `[MenuItem("LionQuest/Generate/...")]` (for human menu click) and a `public static void Generate()` (for `AgentBatch.RunGenerator` to call directly). This dual-entry pattern is required for all generators.

## Key Packages

Use the new **Input System**, not legacy `Input`. UI is **uGUI (Canvas)**, not UI Toolkit — see `doc/startScreen.md` for rationale.

## Architecture

Scripts live flat in `Assets/Scripts/` (**no namespaces** — global namespace throughout). Deep dive with concrete formulas and call chains is in `doc/project.md`; read it before touching combat, saves, or progression.

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
