# Animation Setup Guide

This guide explains how to set up walking animations for your 3D character model.

## Overview

The animation system consists of:
- **Animator Component**: Unity's animation controller (manages animation states)
- **CharacterAnimator Script**: Handles animation parameters based on movement
- **TopDownPlayerController**: Automatically updates animations when the character moves

## Quick Setup

### Option 1: Automatic Setup (Recommended)

1. Select your Player GameObject in the Hierarchy
2. In the `CharacterSetupHelper` component, check the **"Setup Animation"** checkbox
3. Click **"Setup Character"** button (or right-click the component and select "Setup Character")
4. This will automatically add:
   - `Animator` component
   - `CharacterAnimator` script
   - Link them together

### Option 2: Manual Setup

1. Select your Player GameObject
2. Add Component → **Animator**
3. Add Component → **Character Animator**
4. In `TopDownPlayerController`, drag the `CharacterAnimator` component to the "Character Animator" field

## Creating the Animator Controller

### Step 1: Create the Controller

1. In the Project window, right-click in `Assets/Characters/` (or any folder)
2. Select **Create → Animator Controller**
3. Name it `PlayerAnimatorController` (or any name you prefer)

### Step 2: Open the Animator Window

1. Go to **Window → Animation → Animator**
2. Select your Player GameObject in the Hierarchy
3. The Animator window will show your controller

### Step 3: Create Animation States

You'll need at least two states:

#### Idle State (Default)
1. In the Animator window, right-click in empty space
2. Select **Create State → Empty**
3. Name it `Idle`
4. Right-click on `Idle` → **Set as Layer Default State** (it should turn orange)
5. If you have an idle animation clip, drag it onto the `Idle` state

#### Walk State
1. Right-click in empty space → **Create State → Empty**
2. Name it `Walk`
3. If you have a walk animation clip, drag it onto the `Walk` state

## Setting Up Parameters

The animation system uses parameters to control state transitions:

1. In the Animator window, click the **Parameters** tab (top left)
2. Click the **+** button to add parameters:

### Option A: Boolean Parameter (Simplest)
- Click **+** → **Bool**
- Name it: `IsWalking`
- This will switch between Idle and Walk states

### Option B: Float Parameter (More Control)
- Click **+** → **Float**
- Name it: `Speed`
- This allows smooth blending between animations based on speed

### Option C: Both (Full Control)
- Add both `IsWalking` (Bool) and `Speed` (Float)
- You can also add `IsSprinting` (Bool) for sprint animations

## Creating Transitions

### Transition: Idle → Walk

1. Right-click on the `Idle` state → **Make Transition**
2. Click on the `Walk` state
3. Select the transition arrow (it should be highlighted)
4. In the Inspector:
   - **Has Exit Time**: Uncheck this (important!)
   - **Exit Time**: Leave at 0
   - **Transition Duration**: Set to `0.1` (for smooth transition)
   - **Conditions**: Click **+** to add a condition:
     - If using `IsWalking`: Set to `IsWalking` = `true`
     - If using `Speed`: Set to `Speed` > `0.1`

### Transition: Walk → Idle

1. Right-click on the `Walk` state → **Make Transition**
2. Click on the `Idle` state
3. Select the transition arrow
4. In the Inspector:
   - **Has Exit Time**: Uncheck this
   - **Transition Duration**: Set to `0.1`
   - **Conditions**: Add condition:
     - If using `IsWalking`: Set to `IsWalking` = `false`
     - If using `Speed`: Set to `Speed` < `0.1`

## Assigning the Controller

1. Select your Player GameObject
2. In the **Animator** component, drag your `PlayerAnimatorController` to the **Controller** field

## Configuring CharacterAnimator

The `CharacterAnimator` script automatically detects which parameters exist in your controller. Make sure the parameter names match:

- **Is Walking Parameter**: Should match your `IsWalking` bool parameter (default: "IsWalking")
- **Speed Parameter**: Should match your `Speed` float parameter (default: "Speed")
- **Is Sprinting Parameter**: Should match your `IsSprinting` bool parameter (default: "IsSprinting")

## Creating Animation Clips (If You Don't Have Them)

If you don't have animation clips yet, you can:

### Option 1: Use Unity's Animation Window
1. Select your character model GameObject
2. Go to **Window → Animation → Animation**
3. Click **Create** to create a new animation clip
4. Record keyframes for the walking animation
5. Repeat for idle animation

### Option 2: Import from External Source
- Import `.fbx` files with animations
- Unity will extract animation clips automatically

### Option 3: Use Animation Assets
- Purchase/download animation packs from the Asset Store
- Import them into your project

## Testing

1. Enter Play Mode
2. Move your character using the input controls
3. The character should:
   - Play the Walk animation when moving
   - Play the Idle animation when standing still
   - Smoothly transition between states

## Troubleshooting

### Animations Not Playing
- Check that the Animator Controller is assigned to the Animator component
- Verify that animation clips are assigned to the states
- Make sure the Animator component is enabled

### Character Not Transitioning Between States
- Verify parameter names match in both Animator Controller and CharacterAnimator
- Check that transitions have conditions set (not just "Has Exit Time")
- Ensure "Has Exit Time" is unchecked for responsive transitions

### Character Stuck in One Animation
- Check that transitions are set up in both directions (Idle ↔ Walk)
- Verify transition conditions are correct (e.g., `IsWalking` = `true` for Idle→Walk)

### No Parameters Found Warning
- Make sure you've created parameters in the Animator Controller
- Verify parameter names match exactly (case-sensitive)
- Check that parameter types match (Bool vs Float)

## Advanced: Adding More Animations

You can extend this system to include:
- **Sprint Animation**: Add `IsSprinting` parameter and Sprint state
- **Directional Animations**: Create separate Walk states for different directions
- **Attack Animations**: Add attack states triggered by input
- **Jump Animations**: If you add vertical movement later

## Summary

The animation system works like this:
1. `TopDownPlayerController` detects movement input
2. It calls `CharacterAnimator.UpdateAnimations()` with movement state
3. `CharacterAnimator` updates Animator parameters (`IsWalking`, `Speed`, etc.)
4. The Animator Controller transitions between states based on parameters
5. The appropriate animation plays

For more information, see the Unity documentation on [Animator Controllers](https://docs.unity3d.com/Manual/class-AnimatorController.html).

