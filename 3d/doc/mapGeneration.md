# Better Terrain & Biomes — Painterly Stylized RPG Look (Unity-native)

## Context

Current terrain looks amateur: flat Unity Terrain, hard-swapped Gemini-generated ground textures with only a box-blur blend, no grass/foliage details, harsh dirt/grass seam, visible terrain grid, no water, no height, flat default URP terrain shader. Target: polished stylized RPG overworld — lush layered grass, gentle rolling height, soft biome blends, lakes with sandy beach shorelines, winding dirt paths, painterly color grade, soft shadows.

**Approach: 100% Unity-native — no Asset Store imports.** Custom URP Shader Graphs (terrain + water) + Unity Terrain detail system + procedural mesh/texture generation. **Rocks & trees skipped this pass** (weakest native area — revisit later).

Key realization: the reference is a **hand-designed overworld**, but the current code is **runtime-procedural**. There is **no procedural/roguelike requirement** in the codebase (verified: no seed, regen, floors, or map variety anywhere; map is generated once at startup in a single scene). This tension drives the two approaches below.

## Current State (confirmed via code read)

- **Terrain**: one runtime Unity `Terrain` + `TerrainCollider`, built by `Assets/Scripts/MapGenerator.cs` (762 lines). Flat (`flatTerrain=true`); Perlin-height path exists but disabled (`GenerateTerrainHeight()` L257). Biomes = random Voronoi blobs (`GenerateBiomes()` L281), hard nearest-center assignment + radius-2 box blur (`SmoothAlphaMap()` L561) as the ONLY real blend. Uses **URP default terrain lit shader** (never sets `materialTemplate`).
- **`TerrainData` is runtime-only** — `new TerrainData()` each launch (`InitializeTerrain()` L159). Nothing painted by hand would survive a play session.
- **GroundType** SO (`Assets/Scripts/GroundType.cs`): texture, normal, tiling, speed mult, isWalkable, isHole. Assets: `Assets/Terrain/{Grass,Dirt,Hole}.asset`. Textures = Gemini PNGs (not tileable, sRGB, 2048).
- **Gameplay API (MUST preserve)**: `TopDownPlayerController.cs:406/410` calls `mapGenerator.GetSmoothedSpeedMultiplierAtPosition()` + `IsHoleAtPosition()`. Backed by `layerIndexToSpeed`/`layerIndexToIsHole` dicts.
- **No detail/tree scatter exists** — greenfield. TerrainData detail/tree prototype fields are empty schema only.
- **URP**: HDR, MSAA, soft shadows (2048, 50m, 4 cascades), `SupportsTerrainHoles=1`, Forward+. `PC_Renderer` has SSAO (0.4/0.3). Depth + Opaque texture ON (needed for water refraction/depth-foam). `SampleSceneProfile.asset` post: Bloom 0.25, Vignette 0.2, Neutral Tonemapping.
- **Camera**: perspective, tilt 60°, Y-rot 45°, dist 15, height 10.
- **Scene**: `Assets/Scenes/SampleScene.unity` (binary-serialized). Holds Terrain+MapGenerator+Directional Light+players. Per `CLAUDE.md`, AI **may** create/modify `.unity` scenes, **preferably via an editor generator script** (code-generated scenes) — so scene edits are allowed but should be done through the generator, not hand-serialized.
- No `Assets/Editor/` folder yet; scripts flat in `Assets/Scripts/`.

---

## The two approaches (analysis)

The single biggest visual win — **terrain shader + water shader + grass details + color grade** — is **identical in both approaches** and independent of how the terrain is authored. The approaches differ only in **layout authoring**: procedural vs hand-designed.

### Approach A — Evolve current code (procedural-first, keep the pipeline)
Keep `MapGenerator` as runtime authority. Rewrite gen logic in place (organic height, coherent lakes, beach rings, path splines, true weighted splat), add shaders, populate `detailPrototypes`, preserve the gameplay query API.

**Pros**
- Gameplay API (speed/hole) preserved, zero churn in `TopDownPlayerController`.
- **No scene edits** — MapGenerator spawns everything at runtime; sidesteps `.cursorrules` `.unity` restriction.
- Fits `.cursorrules` (fatter scripts, code-driven, no manual editor steps).
- Regeneratable/parameterized — free if procedural variety is ever wanted.
- Incremental: ship height → blend → water → details, each a visible win.

**Cons**
- **Procedural ≠ art-directed.** Random splines + Voronoi never look as *composed* as the reference's deliberate layout. Result reads "nice generic nature," not "designed level."
- Splatmap paint has a hard ceiling — painterly look leans on the shader, not layout.
- Rewriting 762 lines while preserving subtle API/dict behavior = regression risk.

### Approach B — From scratch (hand-authored overworld)
Treat the reference as what it is — an authored level. Persist a `TerrainData` **asset** (not runtime-new), sculpt/paint by hand in Unity's terrain tools, place a water mesh in-scene, and replace MapGenerator's queries with a thin ~80-line `TerrainGroundQuery` reading the saved alphamap.

**Pros**
- **Matches the target's nature** — art-directed layout, the only way to truly hit the reference's composition.
- Full Unity terrain toolset (sculpt/splat/grass brushes) — far more control than code.
- Terrain saved as asset → instant load, no per-launch gen cost, artist-iterable.
- Much less code (delete gen, keep a small query shim).

**Cons**
- **Requires `.unity` scene + new persisted-asset edits** — hits `.cursorrules` "no scene edits without approval," and much is manual editor work (terrain can't be painted well via code).
- Loses procedural regeneration entirely.
- Throws away working, debugged gameplay code (speed/hole blend, hole-fall).
- More upfront manual effort from the user (sculpting/painting).
- One map only — more maps = more hand-authoring.

### Recommendation — Hybrid, leaning A; defer the layout fork
Do the **look work first** (shaders + details + water + grade) via Approach A's runtime pipeline — biggest bang, no scene edits, no risk, and it's reusable no matter how layout is later authored. **Defer the procedural-vs-authored layout decision.** If the composed look of the reference becomes the priority, switch layout to Approach B later **without redoing any shader/detail/water work**.

---

## Execution Plan (chosen: Approach A look-first, layout deferred)

### Phase 1 — Look wins (no scene edits, no layout change) — DO FIRST
Biggest visual jump. Works on the existing flat blob terrain.

1. **URP Terrain Shader Graph** — `Assets/Shaders/StylizedTerrain.shadergraph` (+ `.mat`). Replaces default terrain lit (assign via `terrain.materialTemplate` in `MapGenerator.InitializeTerrain()` — new code, currently unset).
   - Authored for Unity terrain splat (URP terrain Shader Graph target / `_Control` + `_Splat0..3`).
   - **Macro color variation**: albedo × large-scale world-space noise (2 octaves) → kills tiling/grid, hand-painted patchy look.
   - **Height/steepness tint**: darken crevices (fake AO), lighten tops. Keep grass normal-map support.
2. **Grass & flower details** — Unity Terrain `DetailPrototype`s.
   - Author 2–3 grass tuft + 2 flower alpha-cut textures **procedurally in an editor script** (no imports) — tinted green/yellow/white.
   - New `MapGenerator.ApplyDetails()`: populate `terrainData.detailPrototypes` + `SetDetailLayer()` — dense on grass, sparse/none on dirt. Enable built-in **waving grass** wind.
3. **Post/atmosphere** — `Assets/Settings/SampleSceneProfile.asset`: add Color Adjustments (saturation +, warm white balance) + mild fog. Keep Bloom/Vignette/Tonemapping.

### Phase 2 — Water + shoreline (runtime-spawned, no scene edit)
4. **URP Water Shader Graph** — `Assets/Shaders/StylizedWater.shadergraph` (+ `.mat`).
   - Depth color gradient (Scene Depth vs surface, depth texture ON), shore foam (small depth-diff band), scrolling ripples, refraction/transparency (URP opaque texture ON).
5. **Lakes in `MapGenerator`**: carve 1–2 low basins below `waterHeight`; **spawn water mesh planes at runtime** (avoids `.unity` edit) at lake centroids sized to region bounds.
6. **Beach ring**: cells within N units of lake edge → new `Sand` GroundType (distance-to-water blend).

### Phase 3 — Organic layout (still procedural, Approach A)
7. **`MapGenerator` gen rewrite** — preserve public API (`GetMapSize`, `GetSmoothedSpeedMultiplierAtPosition`, `IsHoleAtPosition`, `GetGroundTypeAtPosition`, `IsMapGenerated`, `GenerateMap`) and `layerIndexToSpeed`/`layerIndexToIsHole` dicts.
   - Gentle multi-octave (domain-warped) Perlin height (low amplitude; keep `flatTerrain` toggle).
   - Winding **paths**: Catmull-Rom/Bezier splines painting `Dirt` in a soft band + slight height dip — replaces random blobs.
   - Adjacency rules (sand-around-water, path-over-grass) replace pure random Voronoi.
   - **True weighted splat blend**: per-cell distance-falloff normalized weights, replacing one-hot + box-blur.

### Phase 4 — (optional, deferred) Hand-authored layout (Approach B)
Only if Phase 3's procedural composition isn't good enough vs the reference. Persist `TerrainData` as asset, hand-paint, swap gen for `TerrainGroundQuery`. **Requires user approval for scene + asset edits.** All Phase 1–2 shader/detail/water work carries over unchanged.

### Supporting
8. **Editor generator** — `Assets/Editor/TerrainContentGenerator.cs`, menu `LionQuest/Generate/Terrain Content` (project convention). One-click: create shader materials, detail/flower textures, new GroundType assets (`Sand`, `Water`), assign terrain material template.
9. **GroundType** — add `isWater` flag / detail-density hint if needed.
10. New GroundType assets `Assets/Terrain/{Sand,Water}.asset` + tileable grass/dirt/sand textures (author procedurally, or retile existing Gemini as fallback).

---

## Scene edits
Per `CLAUDE.md`, AI may create/modify `.unity` scenes, **preferably via an editor generator script**. Phase 1–3 mostly avoid scene edits anyway (runtime spawning, code-assigned material), keeping risk low. Where scene edits help — directional light angle/color + ambient/fog, MapGenerator serialized-field wiring — do them through `TerrainContentGenerator` (code-generated) rather than hand-editing the binary scene. **Phase 4** (hand-authored terrain) needs scene + persisted-asset edits, still best driven/scaffolded by the generator.

## Files
| File | Action |
|---|---|
| `Assets/Shaders/StylizedTerrain.shadergraph` + `.mat` | New — painterly terrain (Phase 1) |
| `Assets/Shaders/StylizedWater.shadergraph` + `.mat` | New — lake water (Phase 2) |
| `Assets/Scripts/MapGenerator.cs` | Add material assign + `ApplyDetails()` (P1), lakes/beach (P2), gen rewrite (P3); keep public API |
| `Assets/Scripts/GroundType.cs` | Add `isWater` / density hint if needed |
| `Assets/Editor/TerrainContentGenerator.cs` | New — one-click content build |
| `Assets/Terrain/{Sand,Water}.asset` + textures | New GroundTypes + tileable textures |
| `Assets/Settings/SampleSceneProfile.asset` | Color grade / fog (Phase 1) |

## Verification
1. Run `LionQuest/Generate/Terrain Content` — confirm shaders/materials/GroundTypes/detail textures created, no console errors.
2. Enter Play in `SampleScene` after **each phase**:
   - P1: existing layout but painterly shader, grass tufts + flowers, warmer grade — should already look dramatically better.
   - P2: a lake with foam shoreline + sandy beach ring.
   - P3: rolling grass, winding dirt path, soft weighted biome blends.
3. Walk player onto dirt → speed drops (`Dirt` 0.608 via `GetSmoothedSpeedMultiplierAtPosition`). Walk into deep water/hole → fall triggers (`IsHoleAtPosition`). Confirms gameplay API intact.
4. Compare against reference from game camera angle (tilt 60/rot 45).
5. SSAO/soft shadows still render; frame rate acceptable with detail density.

## Out of scope (this pass)
- Rocks, trees, bushes (procedural meshes — later pass).
- Named landmarks (King's Lake etc.), map labels.
- Cave/cliff geometry, waterfalls.
