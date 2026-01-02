# Animation Debugging Guide

## Problem: Always Playing Walk Animation, Never Idle

If your character is always playing the Walk animation and never transitions to Idle, check these:

### Step 1: Verify Speed Parameter is Being Set

1. **Enable Debug Logging**:
   - Select your character GameObject
   - In `CharacterAnimator` component, check **"Debug Log"** checkbox
   - Enter Play Mode
   - Check the Console window
   - You should see messages showing Speed parameter updates

2. **What to look for**:
   - When **not moving**: Speed should be `0.0`
   - When **moving**: Speed should be `1.0`
   - If Speed is always `1.0` or always `> 0.1`, that's the problem

### Step 2: Check Animator Controller Transitions

1. **Open Animator Window**: `Window > Animation > Animator`
2. **Select your character GameObject** (so the Animator window shows your controller)
3. **Check the transitions**:

   **Walk → Idle transition**:
   - Click on the transition arrow from Walk to Idle
   - In Inspector, verify:
     - ✅ **Has Exit Time**: Unchecked
     - ✅ **Conditions**: Should have `Speed < 0.1` (or `Speed == 0`)
     - ✅ **Transition Duration**: Around 0.1 seconds

   **Idle → Walk transition**:
   - Click on the transition arrow from Idle to Walk
   - In Inspector, verify:
     - ✅ **Has Exit Time**: Unchecked
     - ✅ **Conditions**: Should have `Speed > 0.1`
     - ✅ **Transition Duration**: Around 0.1 seconds

### Step 3: Check Animator Parameters

1. **In Animator window**, click **Parameters** tab
2. **Verify `Speed` parameter exists**:
   - Should be a **Float** type
   - Default value should be `0`

### Step 4: Test in Play Mode

1. **Enter Play Mode**
2. **Open Animator window** (keep it open while playing)
3. **Watch the Animator window**:
   - When you **don't press movement keys**: Should show **Idle** state (orange)
   - When you **press movement keys**: Should transition to **Walk** state
   - When you **release keys**: Should transition back to **Idle** state

4. **Check the Parameters panel** in Animator window:
   - When not moving: `Speed` should show `0`
   - When moving: `Speed` should show `1`

### Step 5: Common Issues and Fixes

#### Issue 1: Speed Parameter Always > 0.1

**Symptoms**: Speed never goes to 0, always stays at 1.0

**Possible causes**:
- `TopDownPlayerController` is always detecting movement
- `isCurrentlyMoving` is always true

**Fix**:
- Check the movement threshold in `TopDownPlayerController`:
  - Line 164: `isCurrentlyMoving = moveDirection.magnitude > 0.1f;`
  - This should correctly detect when there's no input
  - If input is always being read, check your Input Actions setup

#### Issue 2: Transition Condition Wrong

**Symptoms**: Speed is 0 but still in Walk state

**Fix**:
- Check Walk → Idle transition condition
- Should be: `Speed < 0.1` (not `Speed == 0` or `Speed <= 0`)
- Unity's float comparison uses thresholds, so `0.1` is safer than `0`

#### Issue 3: Has Exit Time Enabled

**Symptoms**: Character stays in Walk even after Speed = 0

**Fix**:
- Make sure **"Has Exit Time"** is **unchecked** on Walk → Idle transition
- Exit Time makes the animation wait until it finishes, which prevents immediate transitions

#### Issue 4: No Transition from Walk to Idle

**Symptoms**: Can transition to Walk, but can't get back to Idle

**Fix**:
- Make sure there's a transition arrow from Walk → Idle
- If missing, create it:
  1. Right-click Walk state → Make Transition
  2. Click on Idle state
  3. Set condition: `Speed < 0.1`
  4. Uncheck "Has Exit Time"

#### Issue 5: CharacterAnimator Not Linked

**Symptoms**: Speed parameter never changes

**Fix**:
- Select your character GameObject
- In `TopDownPlayerController` component:
  - Make sure `Character Animator` field has the `CharacterAnimator` component assigned
  - Or leave it empty (it will auto-find it)
- Verify `CharacterAnimator` component exists on the character

### Step 6: Manual Test

To manually test if the Animator Controller works:

1. **Select your character** in Play Mode
2. **In Animator window**, manually set `Speed` parameter:
   - Set `Speed = 0` → Should transition to Idle
   - Set `Speed = 1` → Should transition to Walk
3. If this works, the problem is in the code (TopDownPlayerController or CharacterAnimator)
4. If this doesn't work, the problem is in the Animator Controller setup

### Quick Fix Checklist

✅ **Speed parameter exists** (Float type, default 0)
✅ **Walk → Idle transition exists** with condition `Speed < 0.1`
✅ **Has Exit Time is unchecked** on Walk → Idle transition
✅ **CharacterAnimator component** is on the character
✅ **CharacterAnimator is linked** to TopDownPlayerController (or auto-found)
✅ **Debug Log enabled** to see Speed values in Console
✅ **Input Actions** are correctly set up (no input = no movement)

### Still Not Working?

If after checking all of the above it still doesn't work:

1. **Check Console for errors**:
   - Look for any error messages about Animator or parameters
   - Check if CharacterAnimator is finding the Speed parameter

2. **Verify parameter names match**:
   - In Animator Controller: Parameter name is exactly `Speed` (case-sensitive)
   - In CharacterAnimator: `speedParameter = "Speed"` (should match exactly)

3. **Check if Animator component is enabled**:
   - Select character GameObject
   - In Animator component, make sure it's enabled (checkbox checked)

4. **Try resetting the Animator**:
   - In CharacterAnimator, you can call `ResetAnimator()` method
   - Or manually set Speed = 0 in Animator window

