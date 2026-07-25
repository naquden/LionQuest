# Better Terrain & Biomes — Painterly Stylized RPG Look (Unity-native)

## Context

Current terrain looks amateur: flat Unity Terrain, hard-swapped Gemini-generated ground textures with only a box blur between them, no grass/foliage, harsh dirt/grass seam, visible terrain grid, no water, no height, default URP terrain shader.

Target: polished stylized RPG overworld — layered grass, gentle rolling height, soft biome blends, lakes with sandy shorelines, winding dirt paths, painterly color grade.

**Approach: 100% Unity-native — no Asset Store imports.** Custom URP Shader Graphs (terrain + water) + Unity Terrain detail system + procedural mesh/texture generation. **Rocks & trees skipped this pass** (weakest native area — revisit later).

There is **no procedural/roguelike requirement** in the codebase (no seed, regen, floors, or map variety anywhere; map is generated once at startup in a single scene). The reference look is a *hand-designed* overworld while the code is *runtime-procedural* — that tension is resolved by doing the look work first (shader/detail/water, layout-agnostic) and deferring the layout-authoring fork.

## Current State (verified against source, line numbers exact)

`Assets/Scripts/MapGenerator.cs` — 763 lines, `[RequireComponent(typeof(Terrain))]` (L8). Single runtime terrain, `TerrainCollider` self-repaired at L164-175.

| Fact | Evidence |
|---|---|
| Flat by default; Perlin height path exists but unreachable | `flatTerrain = true` L28; `GenerateFlatTerrain()` L238 zeroes all heights (L247); `GenerateTerrainHeight()` L257 only via `else` L220 |
| Biomes are **discs, not Voronoi** — overlaps resolve by list order, not distance | `GenerateBiomes()` L281; `GetClosestBiomeType()` L340 early-returns inside first matching disc (L356-360) |
| Only real blend is an unweighted radius-2 box blur | `SmoothAlphaMap()` L561, `radius = 2` L568, mean at L593, called once L556 |
| `biomeBlendDistance` (L41) and `biomeSizeVariation` (L38) are **inert** | L372-399 recomputes the same hard single type and returns it — dead code |
| `terrain.materialTemplate` is never assigned → stock URP terrain lit | zero hits for `materialTemplate` in `Assets/Scripts/` |
| TerrainData is **reused and mutated in place**, not recreated | L155-161: `new TerrainData()` is a fallback guarded by `if (terrainData == null)`; the scene's Terrain has a serialized `m_TerrainData`. Regenerated per launch: size L206, heights L251, layers L512, alphamaps L558 |
| Heightmap/alphamap resolution are **read, never set** | L210-211 — they come from the serialized TerrainData (513 / 512 for a fresh one) |
| Map size 200×200, height 20 | `mapSize` L13, `terrainHeight = 20f` L24, applied L206 |
| No detail or tree prototypes anywhere — greenfield | zero hits for `detailPrototypes` / `treePrototypes` / `SetDetailLayer` |
| No runtime instantiation at all — no water/mesh spawning exists | no `Instantiate` / `new Mesh`; only Terrain+Collider `AddComponent` self-repair |
| No editor-only paths, no bake step | no `#if UNITY_EDITOR`, no `[ExecuteInEditMode]`; only `[ContextMenu("Generate Map")]` L181 |

**Public API (complete — must be preserved).** No public fields or properties; every serialized field is `private`.
`GetMapSize()` L18 · `GenerateMap()` L182 · `IsMapGenerated()` L603 · `GetSmoothedSpeedMultiplierAtPosition(Vector3)` L617 · `IsHoleAtPosition(Vector3)` L667 · `GetGroundTypeAtPosition(Vector3)` L699.
Only consumer is `TopDownPlayerController.cs:403,406,410` (`IsMapGenerated`, `GetSmoothedSpeedMultiplierAtPosition`, `IsHoleAtPosition`). `GetMapSize` / `GetGroundTypeAtPosition` have **no callers**.
Backed by `layerIndexToSpeed` / `layerIndexToIsHole` (L64-65), filled at L498-499 (generate path) and L118-119 (rebuild path), read at L646 / L686.

**`GroundType.cs`** (`[CreateAssetMenu("Game/Ground Type")]` L7) — actual field names: `groundName` L12, `biomeColor` L15, `movementSpeedMultiplier` L20 (**`[Range(0,2)]`**), `isWalkable` L23, `isHole` L27, `fallSpeedMultiplier` L31 (`[Range(0.1,5)]`), `terrainTexture` L35, `normalMap` L38, `textureTiling` L41 (default 15,15). No mask/smoothness/height fields, no per-layer tint. Assets: `Assets/Terrain/{Grass,Dirt,Hole}.asset`; textures are Gemini PNGs (not tileable, sRGB, 2048).

**Environment.** URP 17.3, HDR, MSAA, soft shadows (2048 / 50m / 4 cascades), `SupportsTerrainHoles=1`, Forward+. `PC_Renderer` has SSAO (0.4/0.3); Depth + Opaque textures ON (needed for water refraction and depth foam). `SampleSceneProfile.asset`: Bloom 0.25, Vignette 0.2, Neutral tonemapping. Camera: perspective, tilt 60°, Y-rot 45°, dist 15, height 10. Scene `Assets/Scenes/SampleScene.unity` is **binary-serialized** (see `CLAUDE.md`) — never text-edit it; scene changes go through an editor generator using the Unity API.

---

## Blockers found in the existing code

These must be fixed **before** any of the look work, or it silently fails. Ordered by severity.

1. **Adding `Sand`/`Water` GroundType assets to the inspector creates no terrain layer.** Layers are derived from *observed* map contents, not from `availableGroundTypes` — `ApplyTerrainTextures()` L464-473 scans `groundTypeMap` into a `HashSet` and builds layers from that (L482-503). Worse, `GetRandomWalkableGroundType()` L743 filters `isWalkable && !isHole`, so a non-walkable Water type can never be placed by the biome pass, hence never gets a layer.
2. **Layer indices are non-deterministic.** `uniqueTypes` is a `HashSet<GroundType>` (L462) and layer order is its iteration order (L486). Any shader, material, or detail-density code keyed to a splat index breaks run to run.
3. **Holes are painted too late — `IsHoleAtPosition()` can never return true.** `GenerateHoles()` runs at L230, *after* `ApplyTerrainTextures()` L227. It writes `groundTypeMap` (L439) but the alphamap was already built and uploaded (L558), and `IsHoleAtPosition()` reads the *alphamap* (L679-693). Pre-existing bug. Any new pass (lakes, beaches) that mutates `groundTypeMap` inherits it unless it runs before L227.
4. **Water on flat terrain z-fights.** All heights are 0 (L247), so a water plane sits exactly at ground level. Height carving must land before water.
5. **Per-frame `Debug.Log` in the hot path.** L650 and L660 log per layer, per call, and `GetSmoothedSpeedMultiplierAtPosition` is called every frame from `TopDownPlayerController.cs:406`. Going 3 → 5 layers scales the spam proportionally.
6. **`GenerateHoles()` height writes are per-pixel and axis-swapped.** L442-444 does a 1×1 `GetHeights`/`SetHeights` round-trip inside a nested loop — copying this for lake carving tanks generation time. It also indexes `SetHeights(nx, ny, …)` while Unity's heights array is `[y, x]`, so it is transposed relative to `groundTypeMap`.
7. **`RebuildRuntimeData()` matches layers by diffuse texture** (L116, breaks on first match) — Sand and Water need distinct textures or speed/isHole get mis-assigned when `generateOnStart = false`. It also never fills `groundTypeMap`, so `GetGroundTypeAtPosition()` returns null on that path (L701-705).
8. **5+ layers cost extra passes.** Unity's stock terrain shader renders 4 layers per pass. Grass+Dirt+Hole+Sand+Water = 5. The custom Shader Graph must be authored for the layer count actually used.
9. **Alphamap weights are not renormalized after the blur** (L593 divides by neighbour count; edge pixels get fewer samples), so columns don't sum to 1. Unity renormalizes on `SetAlphamaps`, and `GetSmoothedSpeedMultiplierAtPosition` divides by `totalWeight` (L659) to compensate. Don't assume weights are already normalized.

---

## Build & verify loop

Every phase below is gated on this. Run from `3d/`.

```sh
# Compile gate — must pass after ANY script edit. Exit 0 = clean.
sh tools/unity-batch.sh Compile

# Run the new generator headlessly
sh tools/unity-batch.sh RunGeneratorByMenuPath "LionQuest/Generate/Terrain Content"

# Editor facts not visible on disk (serialization mode, scene loadability)
sh tools/unity-batch.sh Diagnose
```

**How the gate works.** `AgentBatch.Compile()` (`Assets/Editor/AgentBatch.cs:21`) only logs `AGENT_BATCH_OK|compile clean` — reaching the method *is* the proof that the whole project compiled, since `-executeMethod` cannot run against a broken assembly. `tools/unity-batch.sh:47` greps the log for `AGENT_BATCH_OK|…` / `AGENT_BATCH_FAIL|…` / `): error CS####` and forwards Unity's exit code.

Constraints, all pre-existing:
- **Batch fails while the Unity editor is open** (`Temp/UnityLockfile` held) — `tools/unity-batch.sh:21` detects it and errors out. Close the editor, or use the MCP bridge (`Tools > Agent Bridge > Start`, then `echo "compile" | nc 127.0.0.1 7891`) for interactive iteration.
- **Batch mode needs the sandbox disabled** — Unity writes to `Library/`, editor prefs, and a licensing mutex outside sandbox-allowed paths. Expected, not a bug.
- Full log at `Logs/agent-batch.log` when the grepped sentinels aren't enough.
- **Shader Graph compile errors do not fail this gate** — Shader Graphs are assets, not C#. They must be checked separately: after generating, read the log for `Shader error` / `ShaderGraph` warnings, and confirm the material renders in Play mode. A pink material means the graph failed.

Register the new generator in `AgentBatch.RunGenerator()` (`Assets/Editor/AgentBatch.cs:43-58`) as a `case "TerrainContent"` alongside the existing `StartScreen` / `CharacterSelection` / `UpgradeScreen` cases, so `sh tools/unity-batch.sh RunGenerator TerrainContent` works too. Per project convention, the generator exposes **both** `[MenuItem("LionQuest/Generate/Terrain Content")]` and `public static void Generate()`.

---

## Phase 0 — Prerequisites (code hygiene, no visual change)

Nothing here changes how the map looks; it makes the rest of the plan possible. All in `Assets/Scripts/MapGenerator.cs`.

1. **Drive terrain layers off `availableGroundTypes`, not observed map contents.** Replace the `HashSet` scan (L462-503) with a deterministic pass over `availableGroundTypes` in list order, skipping entries with no `terrainTexture`. Fixes blockers 1 and 2 together: every configured type gets a stable, inspector-ordered layer index whether or not the generator placed it. Keep `groundTypeToLayerIndex`, `layerIndexToSpeed`, `layerIndexToIsHole` populated exactly as now (L495-499) so the gameplay API is untouched.
2. **Move `GenerateHoles()` before `ApplyTerrainTextures()`** — swap L227 and L230 so hole cells exist in `groundTypeMap` before the alphamap is built. Fixes blocker 3; makes `IsHoleAtPosition()` work for the first time.
3. **Batch the hole height writes.** In `GenerateHoles()` (L442-444), read the heightmap once before the loop, write cells into the local array, and `SetHeights(0, 0, …)` once after. Fix the transposed index while there (`[y, x]`, not `[x, y]`). Fixes blocker 6 and gives lake carving a pattern worth copying.
4. **Delete the dead blend path** — L372-399 plus the now-unused `biomeBlendDistance` (L41) and `biomeSizeVariation` (L38) if nothing else reads them. Removing inert knobs prevents Phase 3 from "tuning" fields that do nothing.
5. **Remove the per-frame logs** at L650 and L660. Keep the one-time generation logs.

**Verify.** `sh tools/unity-batch.sh Compile` → exit 0. Then Play: player speed still drops on dirt (`Dirt` ≈ 0.608 via `GetSmoothedSpeedMultiplierAtPosition`), and walking into a hole now triggers the fall (`IsHoleAtPosition` — previously impossible). Console is quiet during movement. Terrain looks identical to before.

## Phase 1 — Look wins (biggest visual jump, works on the existing flat layout)

6. **Editor generator** — new `Assets/Editor/TerrainContentGenerator.cs`, menu `LionQuest/Generate/Terrain Content`, plus `public static void Generate()` and the `AgentBatch` case. One click creates: the terrain material, the procedural detail textures, and the new GroundType assets. Build this first — the rest of Phase 1 is its output.
7. **Stylized terrain Shader Graph** — `Assets/Shaders/StylizedTerrain.shadergraph` + `.mat`, authored against the URP terrain target (`_Control` + `_Splat0..n`). Assign via `terrain.materialTemplate` in `InitializeTerrain()` (new code — currently never set).
   - **Macro colour variation**: albedo × large-scale world-space noise (2 octaves) — this is what kills the tiling and the visible grid, and is the single biggest win in the whole plan.
   - Height/steepness tint: darken crevices as fake AO, lighten tops. Keep normal-map support.
   - Author for the layer count Phase 0 fixes as deterministic (blocker 8).
8. **Grass & flower details** — Unity Terrain `DetailPrototype`s, greenfield.
   - Generate 2–3 grass tuft + 2 flower alpha-cut textures procedurally in `TerrainContentGenerator` (no imports), tinted green/yellow/white.
   - New `MapGenerator.ApplyDetails()`: populate `terrainData.detailPrototypes` and `SetDetailLayer()` — dense on grass, sparse on dirt, none on hole/water. Keyed to the now-stable layer indices. Enable built-in waving-grass wind.
9. **Post/atmosphere** — `Assets/Settings/SampleSceneProfile.asset`: add Color Adjustments (saturation up, warm white balance) and mild fog. Keep the existing Bloom / Vignette / Neutral tonemapping.

**Verify.** Compile gate, then run the generator headlessly and confirm no `AGENT_BATCH_FAIL` and no `Shader error` in `Logs/agent-batch.log`. Play: same layout, but painterly shading, grass tufts and flowers, warmer grade. Material must not be pink. Check frame rate with detail density on; confirm SSAO and soft shadows still render.

## Phase 2 — Height first, then water

Reordered from the original plan: water **requires** height, or it z-fights (blocker 4).

10. **Gentle rolling height** — replace `GenerateFlatTerrain()` usage with multi-octave, domain-warped Perlin at low amplitude. Keep the `flatTerrain` toggle as an escape hatch. Reuse the batched heightmap write from Phase 0 step 3.
11. **Lake basins** — carve 1–2 regions below a new `waterHeight` threshold in the same height pass.
12. **Water Shader Graph** — `Assets/Shaders/StylizedWater.shadergraph` + `.mat`: depth colour gradient (Scene Depth vs surface), shore foam from a small depth-difference band, scrolling ripples, refraction via the URP opaque texture. Both required textures are already enabled on `PC_Renderer`.
13. **Water surface** — spawn mesh planes at runtime from `MapGenerator` at lake centroids, sized to region bounds. Runtime spawning avoids touching the binary scene.
14. **Sand shoreline** — new `Sand` GroundType painted in a ring by distance-to-water. Must run **before** `ApplyTerrainTextures()` (blocker 3). Give Sand and Water distinct diffuse textures (blocker 7). Note `movementSpeedMultiplier` is `[Range(0,2)]` — enough for sand drag, so no `GroundType` change is needed unless water wants a value outside that.

**Verify.** Compile gate. Play: a lake with a foam shoreline and a sandy ring, no z-fighting at the waterline, water refracts the terrain below. Re-walk the dirt-speed and hole-fall checks — the new passes mutate `groundTypeMap`, so the gameplay API is the thing most likely to regress.

## Phase 3 — Organic layout (still procedural)

15. **Winding paths** — Catmull-Rom or Bezier splines painting `Dirt` in a soft band with a slight height dip, replacing the random discs.
16. **Adjacency rules** — sand-around-water, path-over-grass — replacing purely random placement.
17. **True weighted splat blend** — per-cell distance-falloff normalized weights in `ApplyTerrainTextures()`, replacing the one-hot write (L545) plus box blur (L556). Preserve the `totalWeight` division in `GetSmoothedSpeedMultiplierAtPosition` (L659) or fix both together (blocker 9).

Public API and both lookup dicts stay unchanged throughout.

**Verify.** Compile gate. Play: rolling grass, a winding dirt path that reads as deliberate, soft biome transitions with no hard seam. Compare against the reference from the game camera angle (tilt 60° / rot 45°). Gameplay API checks again.

## Phase 4 — Hand-authored layout (deferred, optional)

Only if Phase 3's procedural composition still falls short of the reference. Persist `TerrainData` as a real asset, hand-paint with Unity's terrain tools, and replace the generator with a thin `TerrainGroundQuery` reading the saved alphamap. Note `GetGroundTypeAtPosition()` returns null on the non-generating path today (blocker 7) — fix before relying on it. **Needs user approval** for scene and persisted-asset edits. All Phase 1–2 shader, detail, and water work carries over unchanged.

## Files

| File | Action |
|---|---|
| `Assets/Scripts/MapGenerator.cs` | Phase 0 fixes; `materialTemplate` assign + `ApplyDetails()` (P1); height/lakes/water spawn/sand (P2); splines + weighted splat (P3). Public API preserved |
| `Assets/Editor/TerrainContentGenerator.cs` | New — one-click content build, dual entry (`[MenuItem]` + `Generate()`) |
| `Assets/Editor/AgentBatch.cs` | Add `case "TerrainContent"` to `RunGenerator()` (L43-58) |
| `Assets/Shaders/StylizedTerrain.shadergraph` + `.mat` | New — painterly terrain (P1) |
| `Assets/Shaders/StylizedWater.shadergraph` + `.mat` | New — lake water (P2) |
| `Assets/Terrain/{Sand,Water}.asset` + tileable textures | New GroundTypes; distinct diffuse textures required |
| `Assets/Settings/SampleSceneProfile.asset` | Colour grade + fog (P1) |
| `Assets/Scripts/GroundType.cs` | Only if a detail-density hint or `isWater` flag proves necessary — prefer not to touch |

## Scene edits

Phases 0–3 avoid the scene entirely: the material is code-assigned, water is runtime-spawned, details are runtime-populated. Where a scene edit does help (directional light angle/colour, ambient/fog, MapGenerator field wiring), do it through `TerrainContentGenerator` using the Unity API — never by text manipulation, since `SampleScene.unity` is binary and its diffs are unreviewable.

## Out of scope (this pass)

- Rocks, trees, bushes (procedural meshes — later pass).
- Named landmarks, map labels.
- Cave/cliff geometry, waterfalls.
- Real terrain holes via `terrainData.SetHoles` — never used today; current "holes" are texture-flagged only.
