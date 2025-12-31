using UnityEngine;

namespace LionQuest.Character
{
    /// <summary>
    /// Simple character controller using force-based movement.
    /// Use arrow keys or WASD to move the character.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveForce = 10f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float drag = 5f;
        
        private Rigidbody2D rb;
        private Vector2 movement;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            // Set Rigidbody2D settings for 2D character
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.linearDamping = drag;
        }
        
        private void Update()
        {
            // Get input
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.y = Input.GetAxisRaw("Vertical");
            movement.Normalize();
        }
        
        private void FixedUpdate()
        {
            // Apply force for movement
            if (movement.magnitude > 0.1f)
            {
                // Only apply force if we're below max speed
                if (rb.linearVelocity.magnitude < maxSpeed)
                {
                    rb.AddForce(movement * moveForce, ForceMode2D.Force);
                }
            }
        }
    }
}

