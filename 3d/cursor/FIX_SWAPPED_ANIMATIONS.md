# Fix: Animations Are Swapped (Walk plays when idle, Idle plays when moving)

## Problem
- Walk animation plays when character is NOT moving
- Idle animation plays when character IS moving

## Solution: Swap the Animation Clips

The animation clips are assigned to the wrong states. Here's how to fix it:

### Step 1: Open Animator Window

1. **Open Animator Window**: `Window > Animation > Animator`
2. **Select your character GameObject** (so the Animator window shows your controller)

### Step 2: Check Current Assignments

1. **Click on the Idle state** (should be orange/default)
2. **In Inspector**, check the "Motion" field
   - Note which animation clip is assigned (probably Walk animation)
3. **Click on the Walk state**
4. **In Inspector**, check the "Motion" field
   - Note which animation clip is assigned (probably Idle animation)

### Step 3: Swap the Animation Clips

#### Option A: Drag and Drop (Easiest)

1. **Find your animation clips** in Project window:
   - Select your FBX model
   - In Inspector > Animation tab
   - You'll see a list of animation clips
   - Identify which is Idle and which is Walk

2. **Assign to Idle state**:
   - Click on **Idle** state in Animator window
   - In Inspector, find "Motion" field
   - **Drag the Idle animation clip** from Project window to the Motion field
   - (Or click the circle icon next to Motion and select Idle animation)

3. **Assign to Walk state**:
   - Click on **Walk** state in Animator window
   - In Inspector, find "Motion" field
   - **Drag the Walk animation clip** from Project window to the Motion field
   - (Or click the circle icon next to Motion and select Walk animation)

#### Option B: Use Circle Icon

1. **Click on Idle state** in Animator window
2. **In Inspector**, click the **circle icon** (target icon) next to "Motion"
3. **Select your Idle animation** from the list
4. **Click on Walk state** in Animator window
5. **In Inspector**, click the **circle icon** next to "Motion"
6. **Select your Walk animation** from the list

### Step 4: Verify

1. **Enter Play Mode**
2. **Don't press movement keys**:
   - Character should play **Idle** animation (standing still)
3. **Press movement keys**:
   - Character should play **Walk** animation (walking)

## How to Identify Which Animation is Which

If you're not sure which animation clip is Idle and which is Walk:

1. **Select your FBX model** in Project window
2. **In Inspector > Animation tab**, you'll see a list of animation clips
3. **Common names**:
   - Idle: `Idle`, `idle`, `Idle_01`, `Standing`, `T-Pose`
   - Walk: `Walk`, `walk`, `Walking`, `Walk_Forward`, `Locomotion`

4. **Preview animations**:
   - Click on an animation clip name in the list
   - You can see the animation preview in the Inspector
   - Idle should show character standing still
   - Walk should show character walking/moving

## Quick Checklist

After swapping:

✅ **Idle state** has **Idle animation clip** assigned
✅ **Walk state** has **Walk animation clip** assigned
✅ When **not moving**: Character plays Idle animation
✅ When **moving**: Character plays Walk animation

## Still Wrong?

If after swapping it's still wrong:

1. **Double-check the animation clip names**:
   - Maybe "Idle" is actually the walk animation
   - Maybe "Walk" is actually the idle animation
   - Try swapping them the other way

2. **Check if you have multiple animation clips**:
   - You might have `Idle_01`, `Idle_02`, etc.
   - Try different ones to see which is correct

3. **Preview the animations**:
   - In FBX Animation tab, click on each animation
   - Watch the preview to identify which is which

