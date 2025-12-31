using UnityEngine;

namespace LionQuest.Character
{
    /// <summary>
    /// Simple character controller for testing animations.
    /// Use arrow keys or WASD to move the character.
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        
        private CharacterAnimator characterAnimator;
        private Rigidbody2D rb;
        private Vector2 movement;
        
        private void Awake()
        {
            characterAnimator = GetComponent<CharacterAnimator>();
            rb = GetComponent<Rigidbody2D>();
            
            // Set Rigidbody2D settings for 2D character
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }
        
        private void Update()
        {
            // Get input
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.y = Input.GetAxisRaw("Vertical");
            movement.Normalize();
            
            // Update animations
            if (characterAnimator != null)
            {
                float speed = movement.magnitude * moveSpeed;
                characterAnimator.SetSpeed(speed);
                characterAnimator.SetWalking(speed > 0.1f);
                
                // Set facing direction
                if (movement.x != 0)
                {
                    characterAnimator.SetFacingDirection(movement.x > 0);
                }
            }
        }
        
        private void FixedUpdate()
        {
            // Apply movement
            rb.linearVelocity = movement * moveSpeed;
        }
    }
}

