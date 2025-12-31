# Character Setup Instructions

## Setting Up Your Character Model as the Player

### Step 1: Import the Model
1. The `3d.obj` file should be automatically imported by Unity
2. If not, Unity will import it when you select it in the Project window

### Step 2: Create the Player GameObject
1. In Unity, go to the Hierarchy
2. Right-click and select **3D Object > Empty GameObject** (or drag the imported model from Project to Hierarchy)
3. Name it "Player"

### Step 3: Add the Character Model
**Option A: If you dragged the model directly:**
- The model is already in the scene

**Option B: If you created an empty GameObject:**
1. Find your `3d` model in the Project window (Assets/Characters/)
2. Drag it onto the Player GameObject in the Hierarchy (this makes it a child)

### Step 4: Set Up the Character
1. Select the Player GameObject (or the root if the model is a child)
2. Add the `CharacterSetupHelper` component
3. Click the "Setup Character" button in the inspector, OR
4. Right-click the component and select "Setup Character"

This will automatically:
- Add a CharacterController component
- Add the TopDownPlayerController script
- Configure the character for top-down gameplay
- Set the tag to "Player"
- Position it correctly

### Step 5: Configure Input Actions
1. Select the Player GameObject
2. In the TopDownPlayerController component, assign the `InputSystem_Actions` asset to the "Input Actions" field
   - You can find it in: Assets/InputSystem_Actions.inputactions

### Step 6: Adjust Character Size (if needed)
1. Select the Player GameObject
2. In the CharacterController component, adjust:
   - **Height**: Height of the character (default: 2)
   - **Radius**: Width/radius of the character (default: 0.5)
   - **Center**: Center point of the controller (should be at half height)

### Step 7: Apply Textures (Optional)
If you want to use the front.png and back.png textures:
1. Create a Material in the Project window
2. Assign the appropriate texture to the material
3. Apply the material to your character model's MeshRenderer

### Notes:
- The character's bottom will be positioned at Y=0 (ground level)
- The CharacterController will prevent the character from moving in the Y-axis
- Make sure your ground plane is at Y=0

