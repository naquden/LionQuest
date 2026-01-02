# Fix: Animation Clip Names Are Wrong

## Problem
Animation clips have incorrect names. For example:
- "Arise" animation shows the death animation when previewed
- Animation names don't match what they actually play

## Solution: Rename Animation Clips in FBX Import Settings

### Step 1: Select Your FBX File

1. **In Project window**, select your FBX file:
   - `AssasinoCappuchino.fbx` (or your character FBX)

### Step 2: Open Animation Tab

1. **In Inspector**, click the **"Animation"** tab
2. **Scroll down** to see the **animation clips list**
   - You'll see all animation clips with their names

### Step 3: Preview Each Animation

1. **Click on each animation clip name** in the list
2. **Watch the preview** in the Inspector (or Scene view)
3. **Identify what each animation actually is**:
   - Note down: "This clip named 'X' actually shows 'Y' animation"
   - Example: "Clip named 'Arise' actually shows death animation"

### Step 4: Rename the Clips

For each animation clip:

1. **Click on the animation clip name** in the list
2. **In the clip settings that appear**, find the **"Name"** field
3. **Change the name** to match what it actually is:
   - If "Arise" shows death → rename it to "Die" or "Death"
   - If "Die" shows arise → rename it to "Arise"
   - Continue for all clips

4. **Example renaming**:
   - Clip named "Arise" (shows death) → Rename to "Die"
   - Clip named "Die" (shows arise) → Rename to "Arise"
   - Clip named "Idle" (shows walk) → Rename to "Walk"
   - Clip named "Walk" (shows idle) → Rename to "Idle"

### Step 5: Apply Changes

1. **After renaming all clips**, click **"Apply"** button at the bottom of Inspector
2. **Wait for Unity to process** the changes

### Step 6: Verify

1. **Click on each renamed clip** again
2. **Preview to verify**:
   - "Idle" should show idle animation
   - "Walk" should show walk animation
   - "Die" should show death animation
   - "Arise" should show arise animation

## Detailed Step-by-Step Example

Let's say you have this situation:
- Clip named "Arise" → Actually shows death animation
- Clip named "Die" → Actually shows arise animation

**To fix:**

1. **Click on "Arise" clip** in the list
2. **In the Name field**, change "Arise" to "Die" (temporary name)
3. **Click on "Die" clip** in the list
4. **In the Name field**, change "Die" to "Arise"
5. **Click on the clip that's now named "Die"** (was "Arise")
6. **In the Name field**, change "Die" to "Arise"
7. **Click "Apply"**

**Result:**
- The clip that showed death is now named "Die"
- The clip that showed arise is now named "Arise"

## Alternative: Create New Clips from Takes

If the FBX has multiple "takes" and you need to reassign them:

1. **Click on an animation clip** in the list
2. **Check the "Take" dropdown**:
   - You might see "Take 001", "Take 002", etc.
   - Each take might have different animation data
3. **Try different takes**:
   - Change the "Take" for a clip
   - Preview to see if it shows the correct animation
   - If yes, keep that take
   - If no, try another take

4. **If you find the correct take**:
   - Keep that take selected
   - Rename the clip to match what it shows
   - Click "Apply"

## Quick Reference: Common Animation Names

When renaming, use these standard names:

- **Idle**: Character standing still
- **Walk** / **Walking**: Character walking
- **Run** / **Running**: Character running
- **Attack**: Character attacking
- **Die** / **Death**: Character dying
- **Arise** / **Respawn**: Character getting up/respawning
- **Jump**: Character jumping
- **Fall**: Character falling

## Tips for Identifying Animations

1. **Watch the preview carefully**:
   - Idle: Character barely moves, might sway slightly
   - Walk: Character's legs move, walking motion
   - Death: Character falls down, stops moving
   - Arise: Character gets up from ground

2. **Check animation length**:
   - Idle: Usually loops, can be any length
   - Walk: Usually 1-2 seconds, loops
   - Death: Usually 2-5 seconds, doesn't loop
   - Arise: Usually 2-4 seconds, doesn't loop

3. **Look at the character's pose**:
   - Death: Character on ground, lying down
   - Arise: Character getting up from ground
   - Idle: Character standing upright
   - Walk: Character in walking pose

## After Renaming: Update Animator Controller

After you've renamed the animation clips:

1. **Open your Animator Controller**
2. **Check each state**:
   - Idle state should use "Idle" animation clip
   - Walk state should use "Walk" animation clip
   - Die state should use "Die" animation clip
   - Arise state should use "Arise" animation clip

3. **If clips are still wrong in Animator**:
   - Click on each state
   - In Inspector, click the circle icon next to "Motion"
   - Select the correctly named animation clip

## Troubleshooting

### Can't See Preview

1. **Make sure you have a scene open**
2. **Drag the FBX to the scene** (temporarily)
3. **Select the GameObject** in scene
4. **Then select the FBX** and preview animations
5. **Or use the Animation window**: `Window > Animation > Animation`

### Multiple Clips with Same Issue

If many clips are wrong:
1. **Make a list** of what each clip actually shows
2. **Rename them all** before clicking Apply
3. **This is faster** than doing one at a time

### Clips Don't Have the Right Animation Data

If previewing shows that NO clip has the animation you need:
- The FBX file might not have that animation
- Check meshy.ai to see what animations were included
- You might need to re-export the FBX with correct animations

## Summary

1. **Select FBX** → **Animation tab**
2. **Click each clip** → **Preview it**
3. **Note what it actually shows**
4. **Rename the clip** to match what it shows
5. **Click Apply**
6. **Update Animator Controller** to use renamed clips

This will fix the animation name mismatches!

