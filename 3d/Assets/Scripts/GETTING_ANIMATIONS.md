# Getting 3D Character Animations

Since your current model (`3d.obj`) is a static mesh without bones, you have several options to add animations:

## Option 1: Use Mixamo (Recommended - FREE & EASY)

**Mixamo** is Adobe's free service that provides rigged characters and animations. This is the easiest solution!

### Steps:

1. **Go to Mixamo**: https://www.mixamo.com/
2. **Sign up** (free account)
3. **Choose a character**:
   - Browse their character library
   - Or upload your own character model (if it's rigged)
   - For quick start, use one of their pre-made characters
4. **Download animations**:
   - Search for "Idle" - download an idle animation
   - Search for "Walking" - download a walking animation
   - Search for "Running" - download a running animation (optional)
5. **Download settings**:
   - Format: **FBX for Unity (.fbx)**
   - Skin: **With Skin** (includes the character)
   - Or **Without Skin** (just the animation if you want to use your own character)
   - Pose: **T-Pose** (for characters) or **A-Pose** (alternative)
6. **Import to Unity**:
   - Drag the downloaded `.fbx` files into your `Assets/Characters/` folder
   - Unity will automatically import them
   - The character will have an Animator Controller ready to use!

### Using Mixamo Characters:

1. Import the Mixamo character `.fbx` into Unity
2. The character will have:
   - A rigged mesh (with bones)
   - An Animator Controller
   - Animation clips
3. Replace your current character model with the Mixamo one
4. Follow the animation setup guide in `ANIMATION_SETUP.md`

### Using Mixamo Animations on Your Character:

If you want to use Mixamo animations on your own character:
1. You'll need to rig your character first (add bones)
2. Or use a tool like **Auto-Rig Pro** or **Mixamo Auto-Rigging**
3. Then download animations "Without Skin" from Mixamo
4. Apply them to your rigged character

## Option 2: Unity Asset Store (Free & Paid)

### Free Options:
1. **Unity Asset Store** → Free Assets
2. Search for "free character animations"
3. Popular free packs:
   - "Free Character Pack" by various creators
   - "Simple Character Controller" (includes animations)
   - "Low Poly Character Pack"

### Paid Options (High Quality):
- **Synty Studios** - Low poly characters with animations
- **Mixamo** - Premium animations
- **Animation Packs** - Various creators

## Option 3: Other Free Resources

### Sketchfab
- https://sketchfab.com/
- Search for "free animated character"
- Many models include animations
- Download as `.fbx` or `.glb`

### CGTrader
- https://www.cgtrader.com/
- Free section has animated characters
- Filter by "Free" and "Animated"

### TurboSquid
- https://www.turbosquid.com/
- Some free animated models available
- Mostly paid, but high quality

### OpenGameArt
- https://opengameart.org/
- Free game assets including animated characters
- Community-driven, various licenses

## Option 4: Use Simple Procedural Animation (Quick Solution)

For your current static `.obj` model, you can use **procedural animations** (code-based movement):

### What This Means:
- Instead of skeletal animations, the model will:
  - Bob up and down when walking
  - Sway slightly side to side
  - Return to normal when idle
- Simple but effective for low-poly or stylized games
- Works immediately with your current model - no rigging needed!

### Quick Setup:

**Option A: Automatic Setup**
1. Select your Player GameObject
2. In `CharacterSetupHelper`, check **"Setup Procedural Animation"**
3. Click **"Setup Character"**
4. Done! The character will now bob when walking

**Option B: Manual Setup**
1. Select your Player GameObject
2. Add Component → **Simple Procedural Animation**
3. Adjust settings in the inspector:
   - **Bob Speed**: How fast it bobs (default: 8)
   - **Bob Amount**: How far it moves up/down (default: 0.1)
   - **Tilt Amount**: How much it tilts side to side (default: 5)
   - **Smoothness**: Animation smoothness (default: 10)

### Customization:
- Adjust the values in the inspector to match your character's style
- For a subtle effect: Lower bob amount (0.05) and tilt amount (2)
- For a more cartoony effect: Increase bob amount (0.2) and tilt amount (10)

## Option 5: Rig Your Current Model

If you want to keep your current character model:

### Tools:
1. **Blender** (Free):
   - Import your `.obj`
   - Add an armature (bones)
   - Rig the character
   - Create animations
   - Export as `.fbx` with animations

2. **Mixamo Auto-Rigging**:
   - Upload your character to Mixamo
   - They'll auto-rig it
   - Download animations

3. **Auto-Rig Pro** (Unity Asset Store):
   - Paid tool that auto-rigs characters in Unity
   - Then you can use Mixamo animations

## Recommendation

**For Quick Start:**
1. Go to **Mixamo.com**
2. Download a free character with "Idle" and "Walking" animations
3. Import to Unity
4. Use that character instead of your current `.obj`
5. Follow `ANIMATION_SETUP.md` to set up the Animator Controller

**For Your Current Model:**
1. Use the `SimpleProceduralAnimation.cs` script (see below)
2. Or rig your model in Blender and use Mixamo animations

## Quick Mixamo Tutorial

1. Visit: https://www.mixamo.com/
2. Click **Characters** → Browse or use default
3. Click on a character you like
4. Click **Download** → Choose:
   - Format: **FBX for Unity**
   - Skin: **With Skin**
5. Download these animations separately:
   - Search "Idle" → Download "Idle" animation
   - Search "Walking" → Download "Walking" animation
6. Import all `.fbx` files to Unity
7. The character `.fbx` will have the model + rig
8. The animation `.fbx` files will have animation clips
9. Create an Animator Controller and assign the clips
10. Done!

## Notes

- **Static meshes** (like `.obj` files) cannot use skeletal animations
- You need a **rigged model** (with bones) for traditional animations
- **Procedural animations** work on any model but are simpler
- **Mixamo** is the easiest solution for beginners

