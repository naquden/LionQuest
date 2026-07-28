# Character Upgrade Screen — Skill Tree (redesign in progress)

## Goal

A screen where a player spends collected **souls** to upgrade a character. Works **identically for
every character** (data-driven — AssCap, and any future character with zero code changes).

Upgradable:

- **Attributes** (Strength/Health/Speed) — repeatable `+1` nodes in an `attributes` pseudo-tree, same
  shape as skill nodes. No separate stat-upgrade UI; levels are **derived** from purchased ranks.
- **Skills** — per-character, per-skill node graphs. Every skill starts **locked**; its tree's root
  `Unlock` node must be purchased before the skill can be used or any of its other nodes bought. Nodes
  are discrete purchases, some **repeatable** (ranked, e.g. `+1 Strength` buyable 5×), some one-shot.

---

## Environment

- Unity **6000.3.2f1** (Unity 6), URP
- uGUI (Canvas) — same UI layer as `startScreen.md` / `characterSelection.md`
- New Input System 1.17.0, TextMeshPro
- Global namespace, static `SaveManager` persistence

---

## Why this redesign (history)

The first implementation (tabs per skill, one-shot node purchases, ALL-of prereqs) shipped but failed
two rounds of review:

1. *"Overlay is completely wrong idea… separate screen with no transparency… larger nodes and
   text… a child should be able to understand this flow."* — fixed: opaque full-screen panel, big
   text, explicit tab buttons.
2. Follow-up screenshot showed **the node area rendering completely blank**. Root cause: node prefab
   used `anchor = (0.5, 0.5)` while `RebuildTree()` positioned nodes as if the container were
   top-left-anchored — every node landed ~1100px right / ~700px below the masked viewport. Arrow-key
   cursor movement still worked (proving data/logic were fine), which is why the description panel
   updated over an apparently empty canvas.

Then reference images set a new bar:

- A **skill-web** reference: circular nodes, rank badges (`0/5`, `3/5`), one colour per branch, links
  bright only on invested paths, per-branch spent totals, all branches visible from one shared root —
  no tabs.
- A **PoE-style hub graph**: one node feeding **many** children (high fan-out), elbow links with
  diamond junctions, three node tiers (keystone / major / minor), rank badges. Explicitly *not* pure
  fan-out — *"a good balanced combination"* of hub, chain, and converging shapes.
- Explicit requirement: attribute points are purchasable, **repeatable** nodes (`+1 Strength`, buyable
  again for more), and prereqs should allow **more nodes accessible, more nodes skippable** navigating
  outward — i.e. **ANY**-of prereqs, not ALL-of.

---

## Locked decisions

| Question | Decision |
|---|---|
| Layout | **Unified canvas** — every branch visible at once, fanning from one shared root at bottom-centre. No tabs. |
| Prereq semantics | **ANY-of** — a node unlocks if at least one `prereqNodeIds` entry has rank ≥ 1. Empty = root. Gives multiple routes in and lets a player skip an unwanted intermediate node. |
| Attributes | Repeatable nodes in an `attributes` tree (`skillSlot = 0`, `target = Attribute`). `strengthLevel`/`healthLevel`/`speedLevel` int save fields are **removed**; levels are derived as `1 + sum(ranks)` of that attribute's nodes. |
| Rank cost | **Flat** — every rank of a repeatable node costs the node's one authored `cost`. |
| Node tiers | Three: `Keystone` (large square — a tree's Unlock/root), `Major` (diamond), `Minor` (circle). |
| Node art | Generator-written PNGs (disc/ring/diamond/square), not hand-authored sprites. |
| Link style | Elbow segments (horizontal-then-vertical) with a diamond junction. Grey when unbought, branch-coloured once the source node has rank ≥ 1. |
| Branch colour | Attributes green, Whirlwind blue, Shuriken purple, Battle Cry red. Header shows souls spent per branch. |
| Graph shape | Each tree mixes **hub** (1→4–5 children, at Keystones and one mid-tree Major), **chain** (linear rank ladders like `cooldown_2s → cooldown_3s`), and **converge** (2–3 prereqs → 1 outer node). Pure fan-out or pure chains are both explicitly rejected. |
| Effect math | Unchanged: stack modifiers scaled by rank — `(SO base + Σ(Add × rank)) × Π(Mul^rank)`. |
| Persistence | Only purchased node IDs **with rank** are saved (`"skillId/nodeId:rank"`). The tree (`CharacterDefinition.skillTrees`) stays the sole source of truth for what a node does and how many ranks it has; resolved values (including derived attribute levels) are rebuilt in memory on load/purchase, never serialized. |

Screen shape (character-select panel):

```
┌──────────────────────────────────────────────────────────────┐
│ Assassino Capuchino                             Souls: 33    │
│ ATTRIBUTES 12   WHIRLWIND 35   SHURIKEN 0   BATTLE CRY 0     │
├───────────────────────────────────────────┬──────────────────┤
│   (0/5)   (3/5)      (1/1)  (0/1)         │ Cooldown -2s     │
│    ·        ·          ·      ·           │   1 / 3          │
│  ╭─┴────────┴─╮      ╭─┴──────┴─╮         │                  │
│  │  ATTRIB    │      │ WHIRLWIND │        │ Whirlwind is     │
│  │  ◆ 5/5     │      │  ◆ 12/12  │        │ ready 2 seconds  │
│  ╰─────┬──────╯      ╰─────┬─────╯        │ sooner.          │
│        ╰────────┬──────────╯              │                  │
│              ( YOU )                      │ Cooldown 10s→8s  │
│                                           │ 15 souls  [BUY]  │
│ [ Back ]                                  │                  │
└───────────────────────────────────────────┴──────────────────┘
```

---

## Data model

### `SkillTreeTypes.cs` (`Assets/Scripts/SkillTreeTypes.cs`)

```csharp
public enum SkillEffect { Unlock, Damage, Cooldown, Area, Duration, Magnitude, ProjectileCount, Strength, Health, Speed }
public enum SkillEffectOp { Add, Mul }
public enum SkillNodeTier { Keystone, Major, Minor }
public enum SkillEffectTarget { Skill, Attribute }

[System.Serializable]
public class SkillNodeDefinition
{
    public string nodeId;
    public string displayName;
    public string description;
    public SkillEffect effect;
    public SkillEffectOp op = SkillEffectOp.Add;
    public float value;
    public int cost = 10;
    public int maxRank = 1;              // >1 = repeatable (e.g. +1 Strength, 5 ranks)
    public SkillNodeTier tier = SkillNodeTier.Minor;
    public string[] prereqNodeIds;       // ANY one satisfies; empty = root
    public Vector2Int gridPos;           // authored layout position
}

[System.Serializable]
public class SkillDisplayBase { public SkillEffect effect; public float baseValue; public string unit; }

[System.Serializable]
public class SkillTreeDefinition
{
    public string skillId;               // "attributes", "whirlwind", "shuriken", "battlecry"
    public string displayName;
    public int skillSlot;                // 0 = non-skill (attributes), 1..3 map to skill1/2/3 input
    public Color branchColor = Color.white;
    public SkillEffectTarget target = SkillEffectTarget.Skill;
    public SkillDisplayBase[] displayBases;   // display-only "before" values, sourced from SO assets
    public SkillNodeDefinition[] nodes;
}
```

The root node is the one with `effect == Unlock` (skill trees) or the free marker node with no prereqs
(`attributes` tree — always reachable, `cost = 0`).

### `CharacterDefinition.cs` (`Assets/Scripts/CharacterSelection/CharacterDefinition.cs`)

Unchanged field: `public SkillTreeDefinition[] skillTrees;` — now includes the `attributes` tree
alongside the three skill trees.

### Save format (`Assets/Scripts/SaveDataTypes.cs`)

```csharp
public class CharacterSaveData
{
    public string characterID;
    public int souls;

    // "skillId/nodeId:rank" keys, e.g. "attributes/strength_1:3", "battlecry/unlock:1".
    // Only persisted skill/attribute state — no more strengthLevel/healthLevel/speedLevel fields.
    public List<string> purchasedSkillNodes = new List<string>();
}
```

`strengthLevel`/`healthLevel`/`speedLevel` are **removed** from the save (and from `CharacterStats`).
`JsonUtility` drops unknown fields, so old saves simply lose those values on load — consistent with the
already-accepted rule that skills (and now attributes) start unlocked-from-zero, no grandfathering.

`SaveManager.SaveCharacter` must drop its manual copy of the three level fields onto the cached entry;
`purchasedSkillNodes` continues to be copied as a fresh `List<string>` (never aliasing the cached
instance `LoadCharacter` returns).

---

## Runtime resolution

### `SkillTreeService.cs` (`Assets/Scripts/SkillTreeService.cs`)

Stateless rules shared by `CharacterStats` (in-game) and `SkillTreeScreenController` (character-select,
operates on `CharacterSaveData` directly — no player has spawned yet).

Key methods:
- `GetRank(save, skillId, nodeId)` — parses the `:rank` suffix, 0 if absent. Replaces the old
  purchased/not-purchased boolean as the primitive.
- `ArePrereqsMet` — **ANY**-of: true if any listed prereq has rank ≥ 1 (or the list is empty).
- `CanPurchase` — prereqs met **and** `GetRank(...) < node.maxRank` **and** enough souls.
- `TryPurchase` — increments the node's rank (rewrites the `:rank` suffix) rather than appending a new
  entry.
- `ResolveSkillValues` — per-`(skillId, effect)` `(add, mul)` totals, each node's contribution scaled by
  its rank: `add += value * rank`, `mul *= pow(value, rank)`.
- `GetValue(resolved, skillId, effect, fallback)` — `(fallback + add) * mul`.
- `GetAttributeLevel(definition, save, effect)` — `1 + Σ(rank)` over nodes in trees with
  `target == Attribute` matching that effect. This is what replaces `strengthLevel` etc.

### `CharacterStats.cs`

```csharp
public CharacterDefinition definition;   // set by PlayerSpawner before spawn

public bool  IsSkillUnlocked(string skillId);
public bool  TryPurchaseNode(string skillId, string nodeId);   // rank+souls+prereq check, save, rebuild cache, fire events
public float GetSkillValue(string skillId, SkillEffect effect, float fallback);
public float GetDamageMultiplier();       // reads derived Strength level
public float GetMaxHealth();              // reads derived Health level
public float GetMoveSpeedMultiplier();    // reads derived Speed level
```

`strengthLevel`/`healthLevel`/`speedLevel` fields and `GetStrengthCost`/`GetHealthCost`/`GetSpeedCost`/
`TryUpgradeStrength`/`TryUpgradeHealth`/`TryUpgradeSpeed` are **deleted** (verified zero callers before
removal). The three derived levels are cached alongside the resolved skill-value dictionary and rebuilt
in the same two places: `LoadStats()` and `TryPurchaseNode()` — never per-frame, never serialized.

### `TopDownPlayerController.cs`

No changes needed beyond the original implementation — it already routes everything through
`GetSkillValue`/`IsSkillUnlocked`/`GetMoveSpeedMultiplier`, all of which keep their signatures.

### `PlayerSlotUI.cs`

`UpdateStats` currently prints `saveData.strengthLevel` etc. directly — switches to
`SkillTreeService.GetAttributeLevel(character, saveData, SkillEffect.Strength)` (and Health/Speed). No
signature change; it already receives both the `CharacterDefinition` and `CharacterSaveData`.

---

## UI

### `Assets/Scripts/UI/SkillTreeNodeUI.cs`

One node widget: tier-specific frame sprite tinted with the branch colour, name, rank badge (`"3/5"`,
or a checkmark when maxed at rank 1), cost (fixed to read `"N souls"`, not a bare number). States:
`Maxed`, `Ranked`, `Affordable`, `Unaffordable`, `Locked`, `Selected` — `Selected` uses a bright
branch-coloured ring, not a white fill (the previous version painted white-on-white and hid the label).

### `Assets/Scripts/UI/SkillTreeScreenController.cs`

Owns the panel. No tabs — builds and lays out **all** trees at once, each offset from a shared root so
branches fan outward. Nodes are anchored top-left (fixes the blank-view bug) and the container is sized
to the true bounds of all spawned nodes, not a fixed oversized guess. Links are elbow segments with a
diamond junction, coloured per-branch once their source node has rank ≥ 1. Cursor navigation is
**spatial** (nearest node in the pressed direction), not a walk through a flat sorted list. Clicking
still only *selects* a node — a separate Buy button spends souls, and re-buying a ranked node advances
it to the next rank. The info panel shows the node's plain-language description, a rank line, and a
before→after value line (e.g. `Cooldown 10s → 8s`) sourced from the tree's `displayBases`.

**Entry point**: unchanged — `CharacterSelectionManager`'s `SlotPhase.Upgrading` phase, opened via the
`UI/Upgrade` action (gamepad North / keyboard Tab). `UpgradeSwitchTab` is removed (no tabs); Navigate
now forwards both axes into the panel's spatial cursor movement instead of one axis driving tab
switching.

---

## Editor generator

`Assets/Editor/SkillTreeScreenGenerator.cs` — `[MenuItem("LionQuest/Generate/Upgrade Screen")]` +
`public static void Generate()`. `AgentBatch.cs` already dispatches `UpgradeScreen` to this menu path.

Generates: node-tier sprites (disc/ring/diamond/square + link-junction diamond) written as PNGs the
same way `TerrainContentGenerator` writes ground textures; a single node prefab whose sprite/size swap
by tier; an elbow-link prefab with a junction; the panel prefab (opaque background, branch-total
header, unified scrollable node canvas, info panel, Buy, Back — no tab row); wires the panel into
`CharacterSelection.unity` (idempotent); authors AssCap's four trees (`attributes`, `whirlwind`,
`shuriken`, `battlecry`) onto `AssCap_Definition.asset`, each mixing hub/chain/converge node shapes and
populating `displayBases` from the real `Skill1Attack.asset`/`ShurikenProjectile.asset`/
`BattleCry.asset` values.

`AgentBatch.DiagnoseSkillTree()` is extended to dump each spawned node's **computed on-screen rect** and
assert it falls inside the viewport bounds — a machine check for the exact blank-view regression seen
in review, not just node/tree counts.

---

## Authored AssCap trees

**attributes** (slot 0, green, `target = Attribute`) — free always-reachable root; `strength_1`/
`health_1`/`speed_1` each `+1`, 5 ranks, 10 souls/rank, hanging off the root; each also feeds an outer
Major node (`strength_2` etc.) for a pricier, bigger jump — 3 ranks, 25 souls/rank.

**whirlwind** (slot 1, blue), **shuriken** (slot 2, purple), **battlecry** (slot 3, red) — each an
`Unlock` Keystone with 4–5 children (hub), incremental rank ladders like `cooldown_2s → cooldown_3s`
(chain), and outer nodes requiring any one of 2–3 inner prereqs (converge). Base values sourced from
`Skill1Attack.asset` (cooldown 10), `ShurikenProjectile.asset` (cooldown 2, damage 25), `BattleCry.asset`
(duration 5, cooldown 10).

---

## Acceptance criteria

- [ ] Skill tree graph is fully visible on open — no node falls outside the viewport (machine-checked
      via `AgentBatch.DiagnoseSkillTree`, not eyeballed).
- [ ] All four branches (attributes + 3 skills) render simultaneously from one shared root; no tabs.
- [ ] A repeatable node (e.g. `+1 Strength`) can be purchased multiple times up to its `maxRank`; badge
      shows current/max rank; cost is flat per rank.
- [ ] A node with 2+ prereqs unlocks once **any one** is purchased — the other prereq path remains
      buyable independently (proves ANY-of, not ALL-of).
- [ ] Every skill starts locked; basic attack always works so a fresh character can farm souls.
- [ ] Attribute levels (Strength/Health/Speed) are correct with **zero** `*Level` fields in the save —
      derived purely from purchased node ranks.
- [ ] Buying/ranking a node spends souls, persists (`purchasedSkillNodes` with rank suffix), and every
      dependent value re-resolves live via `OnStatsUpgraded`/cache rebuild.
- [ ] Info panel shows a plain-language description plus a before→after value for skill nodes.
- [ ] Old saves (previous `purchasedSkillNodes` without rank suffix, or with removed `*Level` fields)
      load without error — by design, no grandfathering; skills/attributes start at zero ranks.

## Out of scope

- Souls economy balancing (authored node costs are placeholders).
- Refund / respec.
- In-game pause-menu entry point (character-select panel covers "unlock before the run").
- Hand-authored node icon art (generator-written primitive shapes stand in for now).
