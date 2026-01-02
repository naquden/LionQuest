# Movement Animation Setup Guide (Unity 6)

This guide explains how to set up Idle and Walk animations that automatically play based on player movement input.

## Overview

The animation system is already integrated with your `TopDownPlayerController`. When you:
- **Press movement keys** → Walk animation plays
- **Release movement keys** → Idle animation plays (loops infinitely)

## Quick Setup Steps

### Step 1: Create Animator Controller

1. **Open Animation Helper Tool**:
   - Go to `Tools > Animation Helper` in Unity menu
   - Select your character GameObject
   - Click "Create New Animator Controller"
   - Save it as `PlayerAnimatorController` in `Assets/Characters/`

   **OR manually create**:
   - Right-click in `Assets/Characters/`
   - Create > Animator Controller
   - Name it `PlayerAnimatorController`

### Step 2: Set Up Parameters

The Animator Controller needs a `Speed` parameter (Float):

1. **Open Animator Window**: `Window > Animation > Animator`
2. **Select your character GameObject** (so the Animator window shows your controller)
3. **Click Parameters tab** (top left)
4. **Add Parameter**:
   - Click `+` button → Float
   - Name it: `Speed`
   - Default value: `0`

### Step 3: Create Animation States

You need two states:

#### Idle State (Default)
1. **Right-click in Animator window** → Create State → Empty
2. **Name it**: `Idle`
3. **Set as Default**: Right-click `Idle` → Set as Layer Default State (should turn orange)
4. **Assign Animation Clip**:
   - Select the `Idle` state
   - In Inspector, find "Motion" field
   - Drag your Idle animation clip from your FBX model
5. **Enable Loop**:
   - **Select your FBX model** in Project window (e.g., `Meshy_AI_Meshy_Merged_Animations.fbx`)
   - In Inspector, click the **Animation** tab (not the Model tab)
   - You'll see a list of animation clips at the bottom
   - **Find your Idle animation** in the list (scroll down if needed)
   - **Click on the Idle animation clip** in the list
   - In the clip settings that appear, check **"Loop Time"** checkbox
   - Click **Apply** button at the bottom of the Inspector

#### Walk State
1. **Right-click in Animator window** → Create State → Empty
2. **Name it**: `Walk`
3. **Assign Animation Clip**:
   - Select the `Walk` state
   - In Inspector, find "Motion" field
   - Drag your Walk animation clip from your FBX model
4. **Enable Loop**:
   - **Select your FBX model** in Project window (same one as before)
   - In Inspector, click the **Animation** tab
   - **Find your Walk animation** in the clips list
   - **Click on the Walk animation clip** in the list
   - In the clip settings, check **"Loop Time"** checkbox
   - Click **Apply** button

### Step 4: Create Transitions

#### Idle → Walk Transition

1. **Right-click `Idle` state** → Make Transition
2. **Click on `Walk` state** (creates transition arrow)
3. **Select the transition arrow** (it highlights)
4. **In Inspector, configure**:
   - **Has Exit Time**: ❌ Unchecked (important for responsive controls!)
   - **Exit Time**: Leave at 0
   - **Transition Duration**: `0.1` (smooth transition)
   - **Conditions**: Click `+` to add condition:
     - Parameter: `Speed`
     - Condition: `Greater`
     - Value: `0.1`
     - This means: "When Speed > 0.1, transition to Walk"

#### Walk → Idle Transition

1. **Right-click `Walk` state** → Make Transition
2. **Click on `Idle` state** (creates transition arrow)
3. **Select the transition arrow**
4. **In Inspector, configure**:
   - **Has Exit Time**: ❌ Unchecked (important for responsive controls!)
   - **Exit Time**: Leave at 0
   - **Transition Duration**: `0.1` (smooth transition)
   - **Conditions**: Click `+` to add condition:
     - Parameter: `Speed`
     - Condition: `Less`
     - Value: `0.1`
     - This means: "When Speed < 0.1, transition to Idle"

### Step 5: Assign Controller to Character

1. **Select your character GameObject** in Hierarchy
2. **Add Animator component** (if not already added):
   - Add Component → Animator
3. **Assign Controller**:
   - In Animator component, drag `PlayerAnimatorController` to "Controller" field
4. **Add CharacterAnimator script**:
   - Add Component → Character Animator
   - The script will automatically detect the `Speed` parameter

### Step 6: Link to Player Controller

1. **Select your character GameObject**
2. **In TopDownPlayerController component**:
   - Drag the `CharacterAnimator` component to the "Character Animator" field
   - (Or leave empty - it will auto-find it)

## How It Works

The system works automatically:

1. **Player presses movement key**:
   - `TopDownPlayerController` detects input
   - Sets `isCurrentlyMoving = true`
   - Calls `characterAnimator.UpdateAnimations(true, 1.0f)`
   - `CharacterAnimator` sets `Speed = 1.0` in Animator
   - Animator transitions from Idle → Walk

2. **Player releases movement key**:
   - `TopDownPlayerController` detects no input
   - Sets `isCurrentlyMoving = false`
   - Calls `characterAnimator.UpdateAnimations(false, 0.0f)`
   - `CharacterAnimator` sets `Speed = 0.0` in Animator
   - Animator transitions from Walk → Idle

3. **Idle animation loops**:
   - Idle state has no exit conditions when Speed = 0
   - Animation clip has "Loop Time" enabled
   - Animation plays infinitely until movement input

## Unity 6 Best Practices

✅ **What we're doing correctly**:

1. **Using Float Parameter (`Speed`)**:
   - Allows smooth blending between states
   - Standard Unity 6 approach
   - Better than boolean for movement animations

2. **No Exit Time on Transitions**:
   - Makes controls feel responsive
   - Transitions happen immediately when input changes
   - Standard for movement animations

3. **Short Transition Duration (0.1s)**:
   - Smooth blending without feeling sluggish
   - Quick enough to feel responsive

4. **Loop Time Enabled**:
   - Idle and Walk animations loop infinitely
   - Standard for movement animations

## Finding Your Animation Clips

To find animation clips from your Meshy AI FBX:

1. **Select your FBX model** in Project window (`Assets/Characters/`)
2. **In Inspector, go to Animation tab**
3. **Scroll through the clips** listed there
4. **Look for**:
   - `Idle` or `idle` animation
   - `Walk` or `walk` or `walking` animation

**OR use the helper tool**:
1. Add `AnimationClipLister` component to any GameObject
2. Drag your FBX to "Model Prefab" field
3. Right-click component → "List Animations from Model"
4. Check Console for all available animations

## Detailed: How to Enable Loop Time (Step-by-Step)

**The "Loop Time" option is in the FBX import settings, not on the animation clip directly.**

### Step-by-Step:

1. **In Unity Project window**, navigate to where your FBX is:
   - If it's in `Assets/Characters/`, go there
   - If it's still in `cursor/character/`, you need to move it to `Assets/Characters/` first

2. **Click on your FBX file** (e.g., `Meshy_AI_Meshy_Merged_Animations.fbx`)
   - The Inspector window will show import settings

3. **Click the "Animation" tab** in the Inspector
   - You'll see tabs like: Model | Rig | Animation | Materials
   - Click on **Animation**

4. **Scroll down** to see the animation clips list
   - At the bottom of the Animation tab, you'll see a list of all animation clips
   - Each clip shows: Name, Start, End, Loop Time checkbox

5. **For each animation you want to loop**:
   - **Click on the animation name** in the list (e.g., click "Idle")
   - The clip settings will appear
   - **Check the "Loop Time" checkbox**
   - Repeat for other animations (Walk, etc.)

6. **Click "Apply"** button at the bottom of the Inspector
   - This saves the changes to the FBX import settings

**Note**: If you don't see the Animation tab or the clips list, make sure:
- You selected the FBX file itself (not a prefab or GameObject)
- The FBX was imported with animations (check Animation tab > "Import Animation" is checked)

## Troubleshooting

### Animation Not Playing When Moving

**Check**:
1. ✅ Animator Controller is assigned to Animator component
2. ✅ Animation clips are assigned to Idle and Walk states
3. ✅ `Speed` parameter exists in Animator Controller
4. ✅ CharacterAnimator component is on the character
5. ✅ CharacterAnimator is linked to TopDownPlayerController
6. ✅ Animator component is enabled

**Debug**:
- Enable "Debug Log" in CharacterAnimator component
- Check Console for parameter updates
- Verify `Speed` parameter is being set (should be 1.0 when moving, 0.0 when idle)

### Character Stuck in One Animation

**Check**:
1. ✅ Transitions exist in both directions (Idle ↔ Walk)
2. ✅ Transition conditions are set correctly:
   - Idle → Walk: `Speed > 0.1`
   - Walk → Idle: `Speed < 0.1`
3. ✅ "Has Exit Time" is unchecked on both transitions
4. ✅ CharacterAnimator is updating parameters correctly

### Idle Animation Not Looping

**Check**:
1. ✅ Select your **FBX model** (not the animation clip) in Project window
2. ✅ Inspector > **Animation** tab (not Model tab)
3. ✅ Find Idle animation in the clips list at the bottom
4. ✅ Click on the Idle animation clip in the list
5. ✅ "Loop Time" checkbox is checked in the clip settings
6. ✅ Click **Apply** button at bottom of Inspector

### Walk Animation Plays But Doesn't Loop

**Check**:
1. ✅ Select your **FBX model** in Project window
2. ✅ Inspector > **Animation** tab
3. ✅ Find Walk animation in the clips list
4. ✅ Click on the Walk animation clip in the list
5. ✅ "Loop Time" checkbox is checked in the clip settings
6. ✅ Click **Apply** button

## Testing

1. **Enter Play Mode**
2. **Press movement keys** (WASD or arrow keys)
3. **Character should**:
   - Play Walk animation
   - Smoothly transition from Idle
4. **Release movement keys**
5. **Character should**:
   - Play Idle animation
   - Smoothly transition from Walk
   - Idle should loop infinitely

## Summary

The movement animation system is **already integrated** and works automatically:

- ✅ `TopDownPlayerController` detects movement input
- ✅ `CharacterAnimator` updates `Speed` parameter
- ✅ Animator Controller transitions between Idle and Walk
- ✅ Idle loops infinitely when not moving
- ✅ Walk plays when movement input is detected

You just need to:
1. Create the Animator Controller
2. Set up Idle and Walk states
3. Assign your animation clips
4. Configure transitions

That's it! The code handles everything else automatically.

