# Texture Setup Guide for Meshy AI Models

## Where Are the Textures?

When you download FBX files from Meshy.ai, textures can be in one of two places:

1. **Embedded in the FBX file** (most common)
2. **Separate texture files** (if meshy.ai exported them separately)

## Finding Textures in Unity

### Method 1: Extract Embedded Textures (Recommended)

If textures are embedded in the FBX file:

1. **Select your FBX model** in the Project window:
   - Navigate to where your FBX is located (e.g., `Assets/Characters/Meshy_AI_Meshy_Merged_Animations.fbx`)
   - Or if it's in `cursor/character/`, you may need to move it to `Assets/Characters/` first

2. **Open the Inspector** (the model should be selected)

3. **Go to the Materials tab**:
   - You'll see tabs like: Model, Rig, Animation, Materials
   - Click on the **Materials** tab

4. **Extract Textures**:
   - Look for an **"Extract Textures"** button
   - Click it to extract all embedded textures
   - Unity will save them next to your FBX file or in a Textures folder

5. **Check the extracted location**:
   - Textures will be saved in the same folder as your FBX
   - Or in a `Textures` subfolder
   - Look for `.png`, `.jpg`, or `.tga` files

### Method 2: Check for Separate Texture Files

If meshy.ai exported textures separately:

1. **Check the download folder**:
   - Look in the folder where you downloaded the FBX from meshy.ai
   - Textures might be in a separate folder or ZIP file
   - Common texture file names:
     - `diffuse.png` or `albedo.png` (base color)
     - `normal.png` (normal map)
     - `roughness.png` or `metallic.png` (PBR maps)

2. **Import textures to Unity**:
   - Drag texture files into `Assets/Characters/` folder
   - Unity will automatically import them

### Method 3: Check Unity's Import Settings

1. **Select your FBX model** in the Project window
2. **In the Inspector, check the Materials tab**:
   - Look at the material list
   - See if textures are already assigned
   - If materials exist but textures are missing, they might need to be extracted

3. **Check the model's materials in the scene**:
   - Drag the FBX into your scene
   - Select the GameObject
   - Check the MeshRenderer component
   - See what materials are assigned
   - Click on the material to see if textures are assigned

## Setting Up Materials in Unity

### If Textures Are Already Extracted:

1. **Select your FBX model** in the Project window
2. **Go to Materials tab** in Inspector
3. **Materials should automatically use the extracted textures**
4. If not, manually assign:
   - Click on each material
   - Drag the texture to the appropriate slot (Base Map/Albedo)

### If You Need to Create Materials Manually:

1. **Create a new Material**:
   - Right-click in `Assets/Characters/` or `Assets/materail/`
   - Create > Material
   - Name it (e.g., "CharacterMaterial")

2. **Assign Textures**:
   - Select the material
   - In Inspector, find the **Base Map** or **Albedo** slot
   - Drag your texture file to it
   - For PBR materials, also assign:
     - **Normal Map** (if you have a normal map texture)
     - **Metallic** and **Smoothness** (if you have metallic/roughness maps)

3. **Apply to Model**:
   - Select your character GameObject in the scene
   - In MeshRenderer component, drag the material to the Materials array

## Common Texture Types

When working with 3D models, you might have:

- **Albedo/Diffuse**: Base color texture (most important)
- **Normal Map**: Adds surface detail
- **Metallic Map**: Defines metal vs non-metal areas
- **Roughness Map**: Defines surface smoothness
- **AO (Ambient Occlusion)**: Adds shadow detail

## Troubleshooting

### Textures Not Showing Up?

1. **Check Material Import Settings**:
   - Select your FBX
   - Materials tab > Material Creation Mode
   - Should be set to "Standard" or "Import Standard Materials"

2. **Check Texture Import Settings**:
   - Select a texture file
   - In Inspector, make sure:
     - **Texture Type** is set correctly (usually "Default" for albedo)
     - **sRGB** is checked for color textures
     - **Normal Map** type is selected for normal maps

3. **Re-import the Model**:
   - Right-click the FBX > Reimport
   - This will refresh materials and textures

### Model Appears White/Gray?

- This usually means textures aren't assigned
- Extract textures using Method 1 above
- Or manually create and assign materials

### Textures Look Wrong?

- Check texture import settings (see above)
- Make sure texture size is appropriate (not too large)
- For normal maps, ensure "Normal Map" texture type is selected

## Quick Setup Steps

1. **Move FBX to Assets folder** (if not already):
   ```
   Assets/Characters/Meshy_AI_Meshy_Merged_Animations.fbx
   ```

2. **Select the FBX** in Unity Project window

3. **Extract Textures**:
   - Inspector > Materials tab > Extract Textures button

4. **Verify Materials**:
   - Check that materials are created and textures assigned
   - If not, manually assign textures to materials

5. **Test in Scene**:
   - Drag model to scene
   - Check if textures appear correctly

## For Meshy.ai Specifically

Meshy.ai typically:
- Embeds textures in the FBX file
- Uses PBR (Physically Based Rendering) materials
- May include multiple texture maps

If textures are missing:
1. Check meshy.ai download page - there might be a separate texture download
2. Extract from FBX using Unity's Extract Textures feature
3. Contact meshy.ai support if textures weren't included in the export

