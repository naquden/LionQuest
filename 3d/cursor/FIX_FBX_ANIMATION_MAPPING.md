# Fix: FBX Animation Mapping Issues

## Problem
Animations from FBX file are incorrectly mapped to the character prefab.

## Solution: Reimport and Fix Animation Settings

### Step 1: Select Your FBX File

1. **In Project window**, navigate to your FBX file:
   - `Assets/Characters/Meshy_AI_Meshy_Merged_Animations.fbx`
   - Or wherever your FBX is located

2. **Click on the FBX file** to select it
   - The Inspector will show import settings

### Step 2: Check Rig Settings

1. **Click the "Rig" tab** in Inspector
2. **Check "Animation Type"**:
   - Should be either **"Humanoid"** or **"Generic"**
   - **Humanoid**: For human-like characters (recommended if your character is humanoid)
   - **Generic**: For other character types
   - **Legacy**: Old format (not recommended)

3. **If it's set to "None"**, that's the problem:
   - Change it to **"Humanoid"** (if human-like) or **"Generic"**
   - Click **Apply**

### Step 3: Check Animation Import Settings

1. **Click the "Animation" tab** in Inspector
2. **Verify these settings**:
   - ✅ **Import Animation**: Should be **checked**
   - ✅ **Animations**: Should show your animation clips listed
   - ✅ **Root Transform Rotation**: Usually "Bake Into Pose" (checked)
   - ✅ **Root Transform Position (Y)**: Usually "Bake Into Pose" (checked)
   - ✅ **Root Transform Position (XZ)**: Usually "Bake Into Pose" (checked)

3. **Check "Bake Animations"**:
   - Should be **checked** for most cases
   - This ensures animations work correctly

### Step 4: Reimport the FBX

1. **Right-click on the FBX file** in Project window
2. **Select "Reimport"**
   - This will reimport the file with current settings
   - Unity will refresh all animations and materials

**OR**:

1. **Select the FBX file**
2. **In Inspector**, click **"Reimport"** button at the bottom

### Step 5: Fix Animation Clip Mapping

If animations are still wrong after reimport:

1. **Select the FBX file**
2. **Click "Animation" tab** in Inspector
3. **Scroll down** to see the animation clips list
4. **For each animation clip**:
   - Click on the animation name in the list
   - Check the settings that appear:
     - **Name**: Should match the animation (Idle, Walk, etc.)
     - **Take**: Usually "Take 001" or similar
     - **Start/End**: Should cover the full animation
     - **Loop Time**: Check if you want it to loop
     - **Root Transform Rotation**: "Bake Into Pose" (checked)
     - **Root Transform Position**: "Bake Into Pose" (checked)

5. **If animations are named incorrectly**:
   - Click on each animation clip
   - Change the **Name** field to the correct name
   - Example: If "Walk" is actually the idle animation, rename it to "Idle"

### Step 6: Check Avatar/Rig Mapping (Humanoid Only)

If using Humanoid rig:

1. **Click "Rig" tab**
2. **Click "Configure..."** button (if available)
   - This opens the Avatar configuration window
3. **Check bone mapping**:
   - Unity should auto-detect bone mapping
   - If bones are incorrectly mapped, you can fix them here
4. **Click "Done"** when finished

### Step 7: Apply Changes

1. **After making changes**, click **"Apply"** button at the bottom of Inspector
2. **Wait for Unity to reimport** (you'll see progress in bottom-right)

### Step 8: Update Prefab/GameObject

1. **If you have a prefab** using this FBX:
   - Select the prefab
   - Check if animations are now correct
   - If not, you may need to:
     - Delete the old prefab
     - Drag the FBX into scene again
     - Create a new prefab

2. **If using the model directly in scene**:
   - Delete the old GameObject
   - Drag the FBX from Project to Scene again
   - Re-setup the character (add components, etc.)

## Common Issues and Fixes

### Issue 1: Animation Type is "None"

**Fix**:
- Go to **Rig** tab
- Change **Animation Type** to **"Humanoid"** or **"Generic"**
- Click **Apply**

### Issue 2: Animations Not Importing

**Fix**:
- Go to **Animation** tab
- Check **"Import Animation"** checkbox
- Click **Apply**

### Issue 3: Wrong Bone Mapping (Humanoid)

**Fix**:
- Go to **Rig** tab
- Click **"Configure..."** button
- Check if bones are mapped correctly
- Unity usually auto-detects, but you can manually fix if needed
- Click **"Done"** then **"Apply"**

### Issue 4: Animations Play But Character Doesn't Move

**Fix**:
- Go to **Animation** tab
- For each animation clip, check:
  - **Root Transform Position (Y)**: Should be "Bake Into Pose" (checked)
  - **Root Transform Position (XZ)**: Should be "Bake Into Pose" (checked)
  - This prevents the animation from moving the root transform

### Issue 5: Animations Are T-Pose or Wrong Pose

**Fix**:
- This might be a rigging issue in the original model
- Check if the model has proper bones/rigging
- Try changing Animation Type:
  - If "Humanoid" doesn't work, try "Generic"
  - If "Generic" doesn't work, try "Humanoid"

## Reimport All Assets

If nothing works, you can force Unity to reimport everything:

1. **Right-click on the FBX file**
2. **Select "Reimport"**
3. **Or**: Go to `Assets > Reimport All` (this reimports everything - takes longer)

## Reset Import Settings

If you've changed settings and want to start fresh:

1. **Select the FBX file**
2. **In Inspector**, click the **gear icon** (top-right of Inspector)
3. **Select "Reset"**
4. **Reconfigure** the import settings from scratch

## Verification

After reimporting:

1. **Select the FBX file**
2. **Click "Animation" tab**
3. **Check the animation clips list**:
   - Should show all your animations
   - Names should be correct
   - Each should have proper Start/End frames

4. **Test in scene**:
   - Drag FBX to scene
   - Add Animator component
   - Assign Animator Controller
   - Enter Play Mode
   - Animations should play correctly

## Still Not Working?

If animations are still incorrectly mapped:

1. **Check the original FBX file**:
   - The issue might be in how meshy.ai exported it
   - Try re-exporting from meshy.ai with different settings

2. **Check Unity version compatibility**:
   - Make sure your Unity version supports the FBX format
   - Unity 6 should support all standard FBX formats

3. **Try importing into a new project**:
   - Create a test scene
   - Import just the FBX
   - See if animations work there
   - This helps isolate if it's a project-specific issue

4. **Check for duplicate animations**:
   - Sometimes FBX files have multiple takes of the same animation
   - Make sure you're using the correct animation clip

