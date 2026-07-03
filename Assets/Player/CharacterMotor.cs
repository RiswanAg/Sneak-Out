using UnityEngine;

namespace CustomCharacterController
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float staminaDrainRate = 10f;
        public float staminaRegenRate = 5f;
        
        [Header("Movement Settings")]
        public float walkSpeed = 1.0f;
        public float runSpeed = 2.0f;
        public float sneakSpeed = 0.5f;
        public float rotationSpeed = 10f;
        
        [Header("Jump Settings")]
        public float jumpHeight = 2f;
        public float gravity = -15f;
        
        [Header("Ground Detection")]
        public float groundCheckDistance = 0.2f;
        public LayerMask groundLayer;
        
        // Public states
        [HideInInspector] public float currentStamina;
        [HideInInspector] public bool isGrounded;
        [HideInInspector] public bool isSprinting;
        [HideInInspector] public bool isSneaking;
        
        // Components
        [HideInInspector] public CharacterController characterController;
        [HideInInspector] public Animator animator;
        
        // Private variables
        private Vector3 velocity;
        private float verticalVelocity;
        
        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            currentStamina = maxStamina;
        }
        
        void Update()
        {
            CheckGroundStatus();
            HandleStamina();
            ApplyGravity();
        }
        
        void CheckGroundStatus()
        {
            // Check if character is grounded
            RaycastHit hit;
            Vector3 spherePosition = transform.position + Vector3.up * 0.1f;
            isGrounded = Physics.SphereCast(spherePosition, characterController.radius * 0.9f, 
                Vector3.down, out hit, groundCheckDistance, groundLayer);
            
            // Reset vertical velocity if grounded
            if (isGrounded && verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }
        }
        
        void HandleStamina()
        {
            if (isSprinting && currentStamina > 0)
            {
                // Drain stamina while sprinting
                currentStamina -= staminaDrainRate * Time.deltaTime;
                currentStamina = Mathf.Max(0, currentStamina);
                
                // Stop sprinting if stamina depleted
                if (currentStamina <= 0)
                {
                    isSprinting = false;
                }
            }
            else if (!isSprinting && !isSneaking && currentStamina < maxStamina)
            {
                // Regenerate stamina when not sprinting and not sneaking
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(maxStamina, currentStamina);
            }
        }
        
        void ApplyGravity()
        {
            if (!isGrounded)
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            
            velocity.y = verticalVelocity;
        }
        
        public void Move(Vector3 direction, float speed)
        {
            if (direction.magnitude > 0)
            {
                // Apply movement with root motion consideration
                Vector3 moveDirection = direction * speed;
                moveDirection.y = verticalVelocity;
                
                // Store for root motion
                velocity = moveDirection;
            }
        }
        
        public void ApplyMovement(Vector3 movement)
        {
            // Apply the final movement with gravity
            movement.y = verticalVelocity;
            characterController.Move(movement * Time.deltaTime);
        }
        
        public void RotateToDirection(Vector3 direction)
        {
            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        public void Jump()
        {
            if (isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        
        public float GetCurrentSpeed()
        {
            if (isSneaking) return sneakSpeed;
            if (isSprinting) return runSpeed;
            return walkSpeed;
        }
    }
}