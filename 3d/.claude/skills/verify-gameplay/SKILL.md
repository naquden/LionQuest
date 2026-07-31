---
name: verify-gameplay
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Agent
description: "Headlessly run and verify LionQuest 3D gameplay/UI flows with real PlayMode tests, synthetic gamepad input, and screenshots read back by the agent. Use whenever a fix or feature claim needs proof beyond 'it compiles' — before saying a bug is fixed, before claiming a UI flow works, or when asked to test/verify/prove a change plays correctly."
---

# Verify Gameplay (PlayMode self-verification harness)

Compiling is not proof. This skill runs the actual game headlessly, drives it with a
synthetic gamepad, asserts on real runtime state, and renders screenshots that get read
back with the `Read` tool — so a "fixed" claim is backed by having actually seen it work.

## When to use this

- Before telling the user a bug is fixed, if the bug is about gameplay/UI *behavior*
  (not just a compile error).
- Before claiming a new feature works end-to-end (join flow, skill tree, combat, HUD, menus).
- Whenever asked to "test", "verify", "prove", or "check this actually works" for anything
  that requires play mode (input, scene transitions, save/load, UI state).
- NOT needed for pure data/asset edits, refactors with no behavior change, or things a
  compile gate (`sh tools/unity-batch.sh Compile`) already proves.

## Model routing (which model runs which phase)

Cost and capability are not the same per phase. Route deliberately:

| Phase | Model | Why |
|---|---|---|
| Think / analyze / diagnose / design the fix / come up with ideas | **Opus 5** (`opus`) | Reasoning over unfamiliar call chains, root-cause work, weighing approaches. Never delegate this down. |
| Write the code (test file, `HarnessFixture` tweak, product fix) once the approach is decided | **Haiku 4.5** (`haiku`) | Mechanical transcription of an already-decided plan. Fast and cheap. |
| Review the written code | **Sonnet 5** (`sonnet`) | Good enough to catch wrong bindings, bad asserts, missed edge cases. |
| Review escalation — as soon as the review starts *proposing fixes* rather than just flagging | **Opus 5** (`opus`) | Proposing a fix is design work again, so it belongs at Opus. Switch mid-review, don't let Sonnet author the redesign. |
| **Run the verification / prove it works** (compile gate, `unity-test.sh`, reading `CAPTURE` PNGs, parsing logs) | **Haiku 4.5** (`haiku`) — most important routing rule | This phase is deterministic tool-driving and sentinel-matching: `AGENT_BATCH_OK`, `AGENT_TEST_OK`, `total=N passed=N failed=0`, then `Read` each screenshot. No reasoning budget needed; the pass/fail signal is in the output. |

How to switch:

- In-session (single-threaded work): tell the user to `/model <opus|sonnet|haiku>`, or change at the phase boundary yourself if the harness allows it.
- Delegated (preferred for the write / review / verify phases): spawn a subagent with the
  `model` parameter set — `Agent(subagent_type: "general-purpose", model: "haiku", ...)` for the
  run/prove phase, `model: "sonnet"` for review, escalating to `model: "opus"` when the review
  turns into a fix proposal.

Hard rules:

- Never let the verify phase run on Opus just because the session happens to be on Opus — it wastes budget on sentinel grepping.
- Never let the diagnose phase run on Haiku. A wrong root cause produces a test that passes while the bug is still live.
- A Haiku verification run that reports `AGENT_TEST_FAIL` hands the failure **back up to Opus** for diagnosis; Haiku does not guess at the fix.

## The pieces (already built, do not recreate)

| Path | Role |
|---|---|
| `tools/unity-test.sh [filter]` | Headless PlayMode runner. `sh tools/unity-test.sh` = all tests. `sh tools/unity-test.sh SkillTree` = `-testFilter` substring match on test/class name. |
| `Assets/Tests/PlayMode/HarnessFixture.cs` | Base class every test inherits. Gives you `AddPad()`, `Press(pad, button)`, `Stick(pad, dir)`, `LoadScene(name)`, `Shot(name)`, `IgnoreFailingLogs()`. |
| `Assets/Tests/PlayMode/*.cs` | Existing suites: `SmokeTests`, `CharacterSelectFlowTests`, `SkillTreeUpgradeTests`. |
| `Assets/Scripts/LionQuest.Runtime.asmdef`, `Assets/Editor/LionQuest.Editor.asmdef`, `Assets/Tests/PlayMode/LionQuest.Tests.PlayMode.asmdef` | Assembly definitions that let tests see game code. Already wired — don't touch unless adding a new package reference. |
| `Logs/test-results.xml` | NUnit3 XML, parsed by the launcher for pass/fail. |
| `screenshots/test-<name>.png` | Screenshots dropped by every `Shot("<name>")` call — read these back, don't just trust the assertion passed. |

## Workflow

Model per step in brackets — see [Model routing](#model-routing-which-model-runs-which-phase).

1. **[opus] Check for an existing test that covers the flow.** `Grep` `Assets/Tests/PlayMode/` for
   the scene/manager/system involved. Extending an existing suite beats writing a new file.
2. **[opus decides the plan → haiku writes it → sonnet reviews] If none exists, write a new `[UnityTest]`** in `Assets/Tests/PlayMode/`, inheriting
   `HarnessFixture`. Pattern:
   ```csharp
   using System.Collections;
   using NUnit.Framework;
   using UnityEngine.InputSystem;
   using UnityEngine.InputSystem.LowLevel;
   using UnityEngine.TestTools;

   public class MyFlowTests : HarnessFixture
   {
       [UnityTest]
       public IEnumerator DoesTheThing()
       {
           IgnoreFailingLogs(); // only if the flow touches CharacterPreviewStage / known noisy paths
           yield return LoadScene("SceneName");
           yield return null;

           Gamepad pad = AddPad();
           yield return Press(pad, GamepadButton.South);
           yield return null;

           Assert.IsTrue(/* real runtime state, not just "no exception" */);
           Shot("my-flow-state");
       }
   }
   ```
   - Look up actual button bindings in `Assets/InputSystem_Actions.inputactions` before
     guessing — action maps (`Player` vs `UI`) bind the same physical button to different
     actions depending on which map is active on that `PlayerInput`.
   - `Stick()` holds a direction across the whole repeat-delay window — use it only when you
     want repeat-fire. For a single nav step, pulse `QueueStateEvent` a Stick value then zero
     it across two frames instead (see `SkillTreeUpgradeTests.cs` for the pattern).
   - Assert on real state: `SaveManager.LoadCharacter(...).souls`, `SkillTreeService.GetRank(...)`,
     `SceneManager.GetActiveScene().name`, `Object.FindObjectsByType<T>(...)`. Never assert
     "no exception was thrown" as the whole test.
3. **[haiku] Compile gate first, always**: `sh tools/unity-batch.sh Compile` — must print
   `AGENT_BATCH_OK`. Never skip straight to running tests on unverified code.
4. **[haiku] Run the test(s)**: `sh tools/unity-test.sh <Filter>`. Must print `AGENT_TEST_OK` with
   a `total=N passed=N failed=0` line. Treat `AGENT_TEST_FAIL` or a missing results file as
   failure — never trust bare exit code (same rule as `unity-batch.sh`).
5. **[haiku] Read every `CAPTURE|...png` path the run printed**, using the `Read` tool. This is the
   step that actually closes the loop — a passing assertion can still hide a visually broken
   screen (blank preview boxes, misaligned UI, wrong color). Only report success after having
   looked at the pixels.
   - **All verification screenshots go to `<project>/screenshots/`** — that is where `Shot()` and
     `AgentBatch.CaptureToPng()` write, and it is the only place they may be written. Never
     `Logs/` (rewritten each run) and never `Temp/` or `temp/` (same directory on this
     case-insensitive filesystem; Unity deletes it on editor quit, so the proof vanishes before
     it can be read). Any new capture path must reuse those two helpers rather than picking its
     own directory. The dir is gitignored — screenshots are proof for the session, not artifacts
     to commit.
6. **[escalate to opus] If a test fails, read `Logs/agent-test.log` and `Logs/test-results.xml`** before assuming
   it's a harness bug. In practice most failures found so far were *real* product bugs
   (see Known gotchas) or a wrong assumption in the test itself (wrong button binding, wrong
   node cost) — rarely the harness.

## Known gotchas (read before debugging a failure from scratch)

- **`Shot()` needs its own capture camera, not the scene's.** Some scene cameras are
  deliberately configured to render nothing (e.g. `CharacterSelection.unity`'s "Main Camera"
  has `cullingMask: 0` — actual visuals come from per-slot preview cameras). `Shot()` already
  spawns a dedicated camera and forces `ScreenSpaceOverlay` canvases to `ScreenSpaceCamera`
  against it — don't try to "simplify" this back to `Camera.main`.
- **`CharacterPreviewStage.Show()` logs a real error/exception every time** a character is
  selected or confirmed in `CharacterSelection` (its `Destroy()` calls on stripped gameplay
  components only take effect end-of-frame, so `Awake()`/`OnEnable()` run once first). This is
  a genuine pre-existing product bug, not a harness fault. Call `IgnoreFailingLogs()` at the
  top of any test that goes through that flow, or the test fails on unrelated log noise.
- **`ScreenCapture.CaptureScreenshotAsTexture()` hangs forever in headless batchmode** — no
  backbuffer is ever presented for `WaitForEndOfFrame` to wait on. `Shot()` avoids this via
  manual `RenderTexture` + `ReadPixels()`. Never revert to `CaptureScreenshotAsTexture`.
- **`Unity.InputSystem.TestFramework` (`InputTestFixture`) is broken in this project** — its
  asmdef declares `versionDefines` twice (second is `[]`), so `UNITY_INCLUDE_TESTS`/
  `UNITY_TESTS_FRAMEWORK` never defines and it never compiles. Don't reference it. Use the
  public `InputSystem.AddDevice<Gamepad>()` / `InputSystem.QueueStateEvent()` API directly
  (already wrapped by `HarnessFixture`).
- **A headless run can hang instead of failing** if a coroutine never yields the frame it's
  waiting on (this happened once with the broken `Shot()`). If `sh tools/unity-test.sh` runs
  past ~90s with no `---- exit` line, don't keep waiting — check
  `ps aux | grep "MacOS/Unity -batchmode"`, and if a Unity process is pinned near 100%+ CPU
  with no progress in `Logs/agent-test.log`, kill it (`pkill -f "MacOS/Unity -batchmode"`) and
  fix the coroutine rather than raising the timeout.
- **Lockfile / licensing zombies** — same rules as `unity-batch.sh` apply to `unity-test.sh`:
  check `Temp/UnityLockfile` is actually held (`lsof`) before assuming the editor is open, and
  check `pgrep -fl "Unity.Licensing.Client"` if a run times out without reaching test output.

## Extending the harness itself

If a new flow genuinely needs a new capability in `HarnessFixture` (not just a new test),
add it there so every suite benefits — don't duplicate input-injection or capture logic
inside an individual test file. Compile-gate after every change to `HarnessFixture.cs`
before running any test, since a mistake there breaks every suite silently.
