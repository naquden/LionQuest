# Top-Down Game Scripts

## TopDownPlayerController.cs
Player controller for 4-directional movement (left, right, up, down) with Y-axis locked.

**Setup:**
1. Add this script to your player GameObject
2. Add a CharacterController component (will be added automatically)
3. Assign the InputSystem_Actions asset to the "Input Actions" field
4. Tag your player GameObject as "Player" (optional, for camera auto-finding)

**Features:**
- 4-directional movement using WASD/Arrow keys
- Sprint functionality (Left Shift)
- Automatic rotation towards movement direction
- Y-axis position locked (no vertical movement)
- Works with Input System

## TopDownCameraController.cs
Tilted isometric camera that follows the player.

**Setup:**
1. Add this script to your Main Camera
2. Assign the player Transform to the "Target" field (or tag player as "Player")
3. Adjust the tilt angle and offset in the inspector

**Features:**
- Smooth camera following
- Adjustable tilt angle (0° = top-down, 90° = side view)
- Isometric rotation angle
- Supports both orthographic and perspective projection

## Quick Start
1. Create a player GameObject (e.g., a capsule or cube)
2. Add TopDownPlayerController script
3. Set up the camera with TopDownCameraController
4. Create a ground plane at Y = 0
5. Play and test!

