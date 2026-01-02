# Unity 6 Animation Controller Setup Guide

This guide explains how to set up a complete animation system for your 3D character using Unity 6 best practices.

## Overview

The animation system consists of:
- **Animator Component**: Unity's animation controller (manages animation states)
- **CharacterAnimator Script**: Enhanced script that handles all animation states (movement, combat, death, etc.)
- **TopDownPlayerController**: Automatically updates movement animations
- **Animator Controller**: State machine with all character states

## Quick Start

### Option 1: Using the Animation Helper Tool (Recommended)

1. **Open the Animation Helper**:
   - Go to `Tools > Animation Helper` in Unity's menu bar
   - Select your character GameObject in the Hierarchy
   - Click "Scan for Available Animations" to see what animations are available from your FBX model
   - Click "Create New Animator Controller" to automatically generate a controller with all standard states

2. **Assign Animation Clips**:
   - Open the Animator window (`Window > Animation > Animator`)
   - Select your character GameObject
   - Drag animation clips from your FBX model onto the corresponding states:
     - `Idle` state → Idle animation clip
     - `Walk` state → Walk animation clip
     - `Attack` state → Attack animation clip
     - `Die` state → Death animation clip
     - `Arise` state → Respawn/Arise animation clip

3. **Verify Setup**:
   - Make sure your character has:
     - `Animator` component (with the controller assigned)
     - `CharacterAnimator` script component
   - The `TopDownPlayerController` should automatically link to `CharacterAnimator`

### Option 2: Manual Setup

Follow the detailed steps below if you prefer manual setup or need to customize the controller.

## Animator Controller Structure

The recommended Animator Controller includes these states:

### States

1. **Idle** (Default State)
   - Character standing still
   - Loops continuously

2. **Walk**
   - Character walking animation
   - Loops while moving

3. **Attack**
   - Attack animation
   - Plays once, then returns to Idle

4. **Die**
   - Death animation
   - Plays once when character dies

5. **Arise**
   - Respawn/revive animation
   - Plays once when character respawns

### Parameters

The controller uses these parameters (all automatically created by the Animation Helper):

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Speed` | Float | Movement speed (0 = idle, >0 = walking) |
| `IsWalking` | Bool | Alternative to Speed for walking state |
| `IsSprinting` | Bool | Sprinting state (optional) |
| `Attack` | Trigger | Triggers attack animation |
| `AttackType` | Int | Type of attack (0 = light, 1 = heavy, etc.) |
| `Die` | Trigger | Triggers death animation |
| `Arise` | Trigger | Triggers respawn animation |
| `IsDead` | Bool | Death state flag |

## Setting Up the Animator Controller Manually

### Step 1: Create the Controller

1. In the Project window, right-click in `Assets/Characters/` (or any folder)
2. Select **Create > Animator Controller**
3. Name it `PlayerAnimatorController` (or any name you prefer)

### Step 2: Add Parameters

1. Open the Animator window (`Window > Animation > Animator`)
2. Click the **Parameters** tab (top left)
3. Add these parameters:

**Float Parameters:**
- `Speed` (Float) - Default: 0

**Bool Parameters:**
- `IsWalking` (Bool) - Default: false
- `IsSprinting` (Bool) - Default: false
- `IsDead` (Bool) - Default: false

**Int Parameters:**
- `AttackType` (Int) - Default: 0

**Trigger Parameters:**
- `Attack` (Trigger)
- `Die` (Trigger)
- `Arise` (Trigger)

### Step 3: Create States

1. In the Animator window, right-click in empty space
2. Create these states:
   - **Idle** (set as default - right-click > Set as Layer Default State)
   - **Walk**
   - **Attack**
   - **Die**
   - **Arise**

3. Assign animation clips to each state:
   - Select a state
   - In the Inspector, drag your animation clip to the "Motion" field

### Step 4: Create Transitions

#### Idle ↔ Walk

**Idle → Walk:**
- Right-click `Idle` → Make Transition → Click `Walk`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Unchecked
  - **Transition Duration**: 0.1
  - **Conditions**: Add `Speed > 0.1`

**Walk → Idle:**
- Right-click `Walk` → Make Transition → Click `Idle`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Unchecked
  - **Transition Duration**: 0.1
  - **Conditions**: Add `Speed < 0.1`

#### Any State → Attack

- Right-click `Any State` → Make Transition → Click `Attack`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Unchecked
  - **Transition Duration**: 0.1
  - **Conditions**: Add `Attack` (trigger)

#### Attack → Idle

- Right-click `Attack` → Make Transition → Click `Idle`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Checked
  - **Exit Time**: 0.9 (attack animation is 90% complete)
  - **Transition Duration**: 0.1
  - **Conditions**: None (or add `Speed < 0.1` to prevent walking immediately after attack)

#### Any State → Die

- Right-click `Any State` → Make Transition → Click `Die`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Unchecked
  - **Transition Duration**: 0.1
  - **Conditions**: Add `Die` (trigger)

#### Die → Arise

- Right-click `Die` → Make Transition → Click `Arise`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Checked
  - **Exit Time**: 0.95 (death animation is 95% complete)
  - **Transition Duration**: 0.2
  - **Conditions**: Add `Arise` (trigger)

#### Arise → Idle

- Right-click `Arise` → Make Transition → Click `Idle`
- Select the transition arrow
- In Inspector:
  - **Has Exit Time**: Checked
  - **Exit Time**: 0.9 (arise animation is 90% complete)
  - **Transition Duration**: 0.1
  - **Conditions**: None

## Using the CharacterAnimator Script

The `CharacterAnimator` script provides methods to control all animations:

### Movement Animations (Automatic)

Movement animations are automatically handled by `TopDownPlayerController`:

```csharp
// Automatically called by TopDownPlayerController
characterAnimator.UpdateAnimations(isMoving, speed, isSprinting);
```

### Combat Animations

```csharp
// Trigger a light attack
characterAnimator.TriggerAttack(0);

// Trigger a heavy attack
characterAnimator.TriggerAttack(1);

// Check if currently attacking
if (characterAnimator.IsAttacking)
{
    // Character is in attack animation
}
```

### Death and Respawn

```csharp
// Trigger death animation
characterAnimator.TriggerDeath();

// Check if dead
if (characterAnimator.IsDead)
{
    // Character is dead
}

// Trigger respawn animation
characterAnimator.TriggerArise();
```

### Reset Animator

```csharp
// Reset all animation states (useful for respawning)
characterAnimator.ResetAnimator();
```

## Example: Complete Character Controller Script

Here's an example of how to use the animation system in your own scripts:

```csharp
using UnityEngine;

public class MyCharacterController : MonoBehaviour
{
    private CharacterAnimator characterAnimator;
    private TopDownPlayerController playerController;
    
    private void Awake()
    {
        characterAnimator = GetComponent<CharacterAnimator>();
        playerController = GetComponent<TopDownPlayerController>();
    }
    
    private void Update()
    {
        // Movement is handled automatically by TopDownPlayerController
        
        // Handle attack input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            characterAnimator.TriggerAttack(0); // Light attack
        }
        
        // Handle death (example)
        if (Input.GetKeyDown(KeyCode.K))
        {
            characterAnimator.TriggerDeath();
        }
        
        // Handle respawn (example)
        if (Input.GetKeyDown(KeyCode.R) && characterAnimator.IsDead)
        {
            characterAnimator.TriggerArise();
        }
    }
}
```

## Finding Available Animations

### Method 1: Using AnimationClipLister Script

1. Add the `AnimationClipLister` component to any GameObject
2. Drag your FBX model to the "Model Prefab" field
3. Right-click the component → "List Animations from Model"
4. Check the Console or the "Animation List" field for all available animations

### Method 2: Using Animation Helper Tool

1. Open `Tools > Animation Helper`
2. Select your character GameObject
3. Click "Scan for Available Animations"
4. View the list of available animations

### Method 3: Manual Inspection

1. Select your FBX model in the Project window
2. In the Inspector, go to the **Animation** tab
3. Scroll through the animation clips listed there

## Importing Animations from FBX

When you import an FBX file with animations:

1. Select the FBX file in the Project window
2. In the Inspector, go to the **Animation** tab
3. Check that **Animation Type** is set correctly:
   - **Humanoid**: For human-like characters (recommended if your model is humanoid)
   - **Generic**: For other character types
   - **Legacy**: Older format (not recommended for new projects)
4. Make sure **Import Animation** is checked
5. Click **Apply**

Unity will automatically extract animation clips from the FBX file.

## Best Practices (Unity 6)

1. **Use Float Parameters for Smooth Blending**: The `Speed` parameter allows smooth transitions between idle and walk states
2. **Disable Exit Time for Responsive Controls**: Uncheck "Has Exit Time" for movement transitions to make controls feel responsive
3. **Use Exit Time for Attack Animations**: Enable "Has Exit Time" for attack animations so they play fully before transitioning
4. **Use Triggers for One-Shot Animations**: Use triggers for attacks, death, and respawn animations
5. **Layer States Properly**: Keep movement states (Idle, Walk) separate from action states (Attack, Die, Arise)
6. **Test Transition Durations**: Adjust transition durations (0.1-0.2 seconds) for smooth blending

## Troubleshooting

### Animations Not Playing

- Check that the Animator Controller is assigned to the Animator component
- Verify that animation clips are assigned to states
- Make sure the Animator component is enabled
- Check that the character GameObject is active

### Character Not Transitioning Between States

- Verify parameter names match exactly (case-sensitive)
- Check that transitions have conditions set
- Ensure "Has Exit Time" is unchecked for movement transitions
- Verify that parameters are being set correctly (enable Debug Log in CharacterAnimator)

### Attack Animation Not Playing

- Make sure the `Attack` trigger parameter exists in the Animator Controller
- Verify that `TriggerAttack()` is being called
- Check that the transition from Any State to Attack is set up correctly
- Ensure the character is not dead (dead characters can't attack)

### Death Animation Not Playing

- Verify the `Die` trigger parameter exists
- Check that `TriggerDeath()` is being called
- Make sure the transition from Any State to Die is set up
- Verify the character isn't already dead

## Advanced: Adding More Animation States

You can extend the system by adding more states:

1. **Sprint State**: Add a Sprint state and transition from Walk when `IsSprinting` is true
2. **Jump State**: Add a Jump state triggered by a Jump trigger
3. **Multiple Attack Types**: Use `AttackType` parameter to blend between different attack animations
4. **Directional Animations**: Create separate Walk states for different directions
5. **Emote States**: Add states for character emotes or special actions

## Summary

The animation system workflow:

1. **Import**: Import FBX model with animations
2. **Create Controller**: Use Animation Helper or create manually
3. **Assign Clips**: Drag animation clips to states
4. **Configure**: Set up transitions and parameters
5. **Script**: Use `CharacterAnimator` methods to control animations
6. **Test**: Play and test all animation states

For more information, see:
- [Unity Animator Controller Documentation](https://docs.unity3d.com/Manual/class-AnimatorController.html)
- [Unity Animation System](https://docs.unity3d.com/Manual/AnimationSection.html)

