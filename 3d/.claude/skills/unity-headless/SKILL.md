---
name: unity-headless
description: Run Unity 6 headlessly for this project — batch CLI commands (compile gate, generators, diagnostics), the interactive MCP Agent Bridge protocol, and the hard-won failure modes of headless batch runs and procedural terrain generation. Use when running tools/unity-batch.sh, invoking a generator, driving the editor over the Agent Bridge socket, or when a headless run hangs, times out, exits 0 without doing anything, or produces a wrong visual result.
---

# Unity headless automation (LionQuest 3D)

Lane selection (which approach for which task) lives in the root `CLAUDE.md`. This skill is
the command reference and failure-mode catalogue for the two headless lanes.

## Lane 1 — Batch CLI headless

```sh
# Compile gate (run after any script edit)
sh tools/unity-batch.sh Compile

# Run a named generator
sh tools/unity-batch.sh RunGenerator StartScreen
sh tools/unity-batch.sh RunGenerator CharacterSelection
sh tools/unity-batch.sh RunGenerator UpgradeScreen
sh tools/unity-batch.sh RunGenerator PlayerHud

# Run any menu item by path
sh tools/unity-batch.sh RunGeneratorByMenuPath "LionQuest/Generate/Start Screen"

# Print editor facts not visible on disk (serialization mode, scene loadability)
sh tools/unity-batch.sh Diagnose
```

Batch mode needs the sandbox disabled — Unity writes to `Library/`, editor prefs, and a
licensing mutex outside sandbox-allowed paths. Expected, not a bug.

**Sentinel lines** grep'd to stdout: `AGENT_BATCH_OK|...` (success) or `AGENT_BATCH_FAIL|...`
(failure). Compile errors surface as `): error CS####`. Exit code 0 = OK, 1 = failure.

**Lock caveat**: batch fails if the Unity editor is open (`Temp/UnityLockfile` exists). The
script detects this and prints a clear error. Close editor first, or use the MCP bridge.

## Lane 2 — MCP bridge (interactive)

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

## Lessons learned (headless batch + terrain generation)

- **Zombie `Unity.Licensing.Client` process blocks all future headless runs.** Killing a batch
  run mid-flight (Ctrl-C, timeout, `kill`) can leave the child `Unity.Licensing.Client` process
  alive holding the global named-pipe mutex (`Unity-LicenseClient-<user>`). Every subsequent
  `-batchmode` launch then times out waiting 60s for that channel and never reaches
  `-executeMethod` — even though `Temp/UnityLockfile` looks free and no `Unity` editor process is
  running. Symptom in `Logs/agent-batch.log`: repeated `Failed to acquire global mutex
  Unity-LicenseClient-atte. Another instance of Unity.Licensing.Client is already running.` Fix:
  `pgrep -fl "Unity.Licensing.Client"` and kill any hit before retrying. Check this whenever a
  batch run hangs or times out, not just when it fails outright.
- **`tools/unity-batch.sh` exit code alone is not proof of success** — it can exit 0 without ever
  reaching `-executeMethod` (licensing failure leaves module assemblies unloaded). Always require
  the `AGENT_BATCH_OK` sentinel in the log, never trust `$?` alone. (The script now enforces this
  itself — see its tail.)
- **A visual/gameplay bug can come from stale Inspector-serialized values, not the code.** After
  rewriting `MapGenerator.cs` correctly (per the design doc), the scene still rendered wrong (two
  flat water blocks, near-zero grass) because the scene's serialized `MapGenerator` component had
  leftover bad values from an earlier iteration: `flatTerrain=true`, `terrainHeight=1` (so every
  height clamped to max), `mapSize=(50,50)` with oversized lake radii. The C# was fine; the
  `.unity` scene's field values were not. **Always dump the actual serialized values from the scene
  headlessly before assuming a code fix will show up** — don't rely on defaults in the script or on
  memory of what you last set. Pattern: add a throwaway `AgentBatch.Diagnose*` method that opens
  the scene, `new SerializedObject(component)`, and `Debug.Log`s each relevant `SerializedProperty`
  (see `AgentBatch.DiagnoseMapGenerator`/`PropVal` for the template — switch on
  `SerializedPropertyType`).
- **A content/wiring generator should force-correct critical scalar fields it depends on, not just
  wire references.** `TerrainContentGenerator.WireSceneMapGenerator()` now overwrites
  `mapSize`/`terrainHeight`/`flatTerrain`/`islandRadius`/`oceanDepth`/`beachWidth` on every run,
  instead of only assigning materials/textures and trusting whatever the Inspector already had.
  Otherwise every regeneration inherits whatever bad state a prior manual edit or earlier generator
  version left behind.
- **You can run arbitrary runtime logic headlessly, not just menu-item generators.** Added
  `AgentBatch.RunGenerator GenerateMap` which opens `SampleScene`, finds the `MapGenerator`
  component, and calls `generator.GenerateMap()` directly (not via `[MenuItem]`), then saves the
  scene. This is how Phase 2/3 procedural logic (lake carving, splines, alphamap blending) got
  verified for actual runtime exceptions headlessly, versus only checking that it compiles. Prefer
  this over trusting "compiles clean" as proof that generation logic works.
- **URP terrain shaders render at most 4 layers per pass.** A 5th needs a separate add-pass shader
  with its own `Dependency` declaration (unverified how well that behaves). Given 4 slots
  (Grass/Dirt/Hole/Sand), water was made a runtime-spawned `Quad` mesh with its own hand-written
  shader instead of a 5th terrain layer.
- **URP terrain/lit shaders ship with `Fallback "Hidden/Universal Render Pipeline/FallbackError"`**,
  which silently renders pink on any shader compile failure with no console error. Deliberately
  omit `Fallback` from hand-written shaders during development so failures surface as real compile
  errors instead of a silent visual bug.
- **`Destroy()` throws outside Play mode.** Any method reachable from `[ContextMenu]` (edit-mode
  callable) that destroys objects needs an edit-mode-safe branch:
  `Application.isPlaying ? Destroy(x) : DestroyImmediate(x)`. See `MapGenerator.DestroySafely`.
- **Runtime-spawned objects (e.g. water plane) survive domain reload as scene children but lose
  their C# list reference.** Don't rely solely on a tracked `List<Transform>` to find/replace them
  on regeneration — sweep by a name prefix (`WaterSurfacePrefix = "WaterSurface_"`) among the
  parent's children first, since the list is empty after reload but the GameObject is still there.
