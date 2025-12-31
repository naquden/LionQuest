using UnityEngine;
using UnityEngine.InputSystem;

namespace LionQuest.Character
{
    /// <summary>
    /// Simple character controller using force-based movement.
    /// Uses Unity's new Input System (works with WASD and arrow keys).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveForce = 10f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float drag = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        private Rigidbody2D rb;
        private Vector2 movement;
        private Keyboard keyboard;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            if (rb == null)
            {
                Debug.LogError("SimpleCharacterController: Rigidbody2D component not found!");
                return;
            }
            
            // Set Rigidbody2D settings for 2D character
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.linearDamping = drag;
            
            // Ensure Rigidbody2D is not kinematic
            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                Debug.LogWarning("SimpleCharacterController: Rigidbody2D is set to Kinematic! Changing to Dynamic.");
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
            
            // Wake up the rigidbody
            rb.WakeUp();
            
            // Get keyboard reference
            keyboard = Keyboard.current;
        }
        
        private void Update()
        {
            // Get input from new Input System
            movement = Vector2.zero;
            
            if (keyboard != null)
            {
                // Horizontal movement (A/D or Left/Right arrows)
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    movement.x -= 1f;
                }
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    movement.x += 1f;
                }
                
                // Vertical movement (W/S or Up/Down arrows)
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    movement.y += 1f;
                }
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    movement.y -= 1f;
                }
            }
            
            movement.Normalize();
            
            if (showDebugInfo && movement.magnitude > 0.1f)
            {
                Debug.Log($"Input detected: {movement}, Magnitude: {movement.magnitude}");
            }
        }
        
        private void FixedUpdate()
        {
            if (rb == null) return;
            
            // Apply force for movement
            if (movement.magnitude > 0.1f)
            {
                // Calculate desired velocity
                Vector2 desiredVelocity = movement * maxSpeed;
                
                // Calculate the force needed to reach desired velocity
                Vector2 velocityChange = desiredVelocity - rb.linearVelocity;
                Vector2 force = velocityChange * moveForce;
                
                // Apply force
                rb.AddForce(force, ForceMode2D.Force);
                
                if (showDebugInfo)
                {
                    Debug.Log($"Applying force: {force}, Current velocity: {rb.linearVelocity}, Desired: {desiredVelocity}");
                }
            }
            else
            {
                // Apply drag when not moving (helps stop the character)
                if (rb.linearVelocity.magnitude > 0.01f)
                {
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * drag);
                }
            }
        }
        
        private void OnValidate()
        {
            // Ensure values are positive
            if (moveForce < 0) moveForce = 10f;
            if (maxSpeed < 0) maxSpeed = 5f;
            if (drag < 0) drag = 5f;
        }
    }
}
