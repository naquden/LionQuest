# Fix: Avatar Rig Configuration Mismatch Error

## Problem
```
Avatar Rig Configuration mis-match. Inbetween bone rotation in configuration 
does not match rotation in animation file: 'neck' : rotation error = 16.061945 deg
```

This error occurs when Unity's Humanoid avatar configuration doesn't match the bone structure in your FBX file.

## Solution 1: Regenerate Avatar (Recommended)

### Step 1: Select Your FBX File

1. **In Project window**, select your FBX file:
   - `AssasinoCappuchino.fbx` (or your character FBX)

### Step 2: Open Rig Tab

1. **In Inspector**, click the **"Rig"** tab
2. **Check "Animation Type"**:
   - Should be set to **"Humanoid"**

### Step 3: Regenerate Avatar

1. **Click "Configure..."** button (or "Configure Avatar" button)
   - This opens the Avatar configuration window
2. **In the Avatar window**:
   - Unity will try to auto-detect bone mapping
   - Check if bones are mapped correctly (green checkmarks)
3. **Click "Done"** to close the Avatar window
4. **Back in Inspector**, click **"Apply"** button
   - This regenerates the avatar with current bone structure

### Step 4: Reimport

1. **Right-click the FBX file** → **"Reimport"**
   - Or click **"Reimport"** button at bottom of Inspector
2. **Wait for reimport to complete**
3. **Check Console** - the error should be gone

## Solution 2: Change to Generic Animation Type

If the model isn't a standard humanoid, or if regenerating doesn't work:

### Step 1: Change Animation Type

1. **Select your FBX file**
2. **Click "Rig" tab** in Inspector
3. **Change "Animation Type"**:
   - From: **"Humanoid"**
   - To: **"Generic"**
4. **Click "Apply"**

### Step 2: Reimport

1. **Right-click FBX** → **"Reimport"**
2. **Check Console** - error should be resolved

**Note**: Generic animation type works for any character, not just humanoids. It's more flexible but doesn't support animation retargeting between different characters.

## Solution 3: Fix Bone Mapping Manually

If you need to keep Humanoid type:

### Step 1: Configure Avatar

1. **Select FBX file**
2. **Rig tab** → **"Configure..."** button
3. **In Avatar window**, check bone mapping:
   - Look for bones with **yellow warnings** or **red errors**
   - The 'neck' bone might be incorrectly mapped

### Step 2: Fix Neck Bone

1. **In Avatar window**, find the **"Neck"** bone in the hierarchy
2. **Check if it's mapped correctly**:
   - Should map to your model's neck bone
   - If it's mapped to wrong bone, Unity will show a warning
3. **Try unmapping and remapping**:
   - Click on the neck bone
   - Clear the mapping
   - Let Unity auto-detect again
   - Or manually map it to the correct bone

### Step 3: Apply and Reimport

1. **Click "Done"** in Avatar window
2. **Click "Apply"** in Inspector
3. **Reimport** the FBX file

## Solution 4: Ignore the Warning (If Animations Work)

If your animations are working correctly despite the warning:

1. **The warning is just a mismatch detection**
2. **It might not affect gameplay** if:
   - Animations play correctly
   - Character moves as expected
   - No visual glitches

3. **To suppress the warning**:
   - Go to **Animation tab**
   - Scroll to animation clips
   - For each clip, you can adjust import settings
   - The warning might persist but won't affect functionality

## Solution 5: Re-export from Source (If Available)

If you have access to the original model:

1. **Re-export the FBX** from your 3D software (Blender, Maya, etc.)
2. **Ensure bone orientations are correct**:
   - Bones should be properly aligned
   - No unusual rotations in bind pose
3. **Export with standard settings**:
   - Use standard FBX export presets
   - Ensure bone hierarchy is clean
4. **Reimport to Unity**

## Which Solution to Use?

### Use Solution 1 (Regenerate Avatar) if:
- ✅ Your character is humanoid
- ✅ You want to use Humanoid animation retargeting
- ✅ This is the first time seeing this error

### Use Solution 2 (Change to Generic) if:
- ✅ Your character is not a standard humanoid
- ✅ You don't need animation retargeting
- ✅ Regenerating avatar doesn't work
- ✅ You want the quickest fix

### Use Solution 3 (Manual Fix) if:
- ✅ You need Humanoid type
- ✅ You know which bones are wrong
- ✅ You want precise control

### Use Solution 4 (Ignore) if:
- ✅ Animations work correctly
- ✅ No visual issues
- ✅ Warning doesn't affect gameplay

## Quick Fix (Recommended)

**For most cases, try this first:**

1. **Select FBX file**
2. **Rig tab** → Change to **"Generic"** (if not humanoid) or click **"Configure..."** then **"Done"** (if humanoid)
3. **Click "Apply"**
4. **Reimport** the file
5. **Check Console** - error should be gone

## Verification

After applying a solution:

1. **Check Console** - no more rig errors
2. **Test animations**:
   - Drag character to scene
   - Add Animator component
   - Assign Animator Controller
   - Enter Play Mode
   - Animations should play correctly
3. **Check for visual issues**:
   - Character should animate smoothly
   - No bone twisting or strange rotations
   - Movements should look natural

## Common Causes

1. **Model exported with non-standard bone orientations**
2. **Avatar configuration was created for a different model**
3. **Bone hierarchy changed after avatar was created**
4. **FBX file has multiple takes with different bone setups**
5. **Model is not a standard humanoid** (should use Generic instead)

## Still Getting Errors?

If the error persists:

1. **Check if multiple FBX files** have the same issue
2. **Try importing into a fresh Unity project** to isolate the issue
3. **Check Unity version compatibility** with your FBX format
4. **Contact meshy.ai support** if the FBX was exported from there - they might need to fix the export settings

