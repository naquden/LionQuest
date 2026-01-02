# Quick Fix: Character Stuck in Walk Animation

## Problem
Character always plays Walk animation, never transitions to Idle.

## Most Common Cause: Missing or Incorrect Transition

### Quick Fix Steps:

1. **Open Animator Window**:
   - `Window > Animation > Animator`
   - Select your character GameObject

2. **Check if Walk → Idle transition exists**:
   - Look for an arrow going from **Walk** state to **Idle** state
   - If there's NO arrow, that's the problem!

3. **Create the transition** (if missing):
   - Right-click on **Walk** state
   - Select **Make Transition**
   - Click on **Idle** state
   - An arrow should appear from Walk to Idle

4. **Configure the transition**:
   - Click on the transition arrow (from Walk to Idle)
   - In Inspector, set:
     - **Has Exit Time**: ❌ **UNCHECKED** (very important!)
     - **Exit Time**: 0
     - **Transition Duration**: 0.1
     - **Conditions**: Click `+` button
       - Parameter: `Speed`
       - Condition: `Less`
       - Value: `0.1`

5. **Test**:
   - Enter Play Mode
   - Don't press any movement keys
   - Character should transition to Idle

## Second Most Common Cause: Has Exit Time Enabled

If the transition exists but still doesn't work:

1. **Click on Walk → Idle transition arrow**
2. **In Inspector, make sure**:
   - **Has Exit Time**: ❌ **UNCHECKED**
   - If it's checked, the animation will wait until Walk finishes before transitioning

## Third Cause: Wrong Transition Condition

1. **Click on Walk → Idle transition arrow**
2. **Check the condition**:
   - Should be: `Speed < 0.1`
   - NOT: `Speed == 0` or `Speed <= 0`
   - Unity's float comparison works better with thresholds

## Debug: Check if Speed Parameter is Being Set

1. **Enable Debug Logging**:
   - Select your character GameObject
   - In `CharacterAnimator` component, check **"Debug Log"**
   - Enter Play Mode
   - Check Console window

2. **What you should see**:
   - When **not moving**: `Speed = 0`
   - When **moving**: `Speed = 1`
   - If Speed is always 1, the problem is in TopDownPlayerController
   - If Speed is 0 but still in Walk, the problem is in Animator Controller transitions

## Visual Check in Play Mode

1. **Enter Play Mode**
2. **Keep Animator window open**
3. **Watch the Animator window**:
   - When you **don't press keys**: Should show **Idle** state highlighted (orange)
   - When you **press keys**: Should transition to **Walk** state
   - If Walk stays highlighted even when not pressing keys, the transition isn't working

4. **Check Parameters panel** (in Animator window):
   - When not moving: `Speed` should show `0`
   - When moving: `Speed` should show `1`

## Still Not Working?

Check these in order:

1. ✅ **Walk → Idle transition exists** (arrow from Walk to Idle)
2. ✅ **Has Exit Time is UNCHECKED** on Walk → Idle transition
3. ✅ **Condition is `Speed < 0.1`** (not `== 0`)
4. ✅ **Speed parameter exists** in Animator Controller (Parameters tab)
5. ✅ **CharacterAnimator component** is on the character
6. ✅ **CharacterAnimator is linked** to TopDownPlayerController
7. ✅ **Animator component is enabled** (checkbox checked)

If all of these are correct and it still doesn't work, the issue might be:
- Input Actions always sending input (check Input System)
- CharacterAnimator not finding the Speed parameter (check parameter name matches exactly)

