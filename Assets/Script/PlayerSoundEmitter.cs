using UnityEngine;

namespace StarterAssets
{
    [RequireComponent(typeof(ThirdPersonController))]
    public class PlayerSoundEmitter : MonoBehaviour
    {
        private ThirdPersonController controller;
        private CharacterController characterController;
        private bool wasGrounded;
        private float footstepTimer;
        private float currentFootstepInterval;

        [Header("Footstep Timing")]
        public float sneakFootstepInterval = 0.6f;
        public float walkFootstepInterval = 0.5f;
        public float runFootstepInterval = 0.35f;

        void Start()
        {
            controller = GetComponent<ThirdPersonController>();
            characterController = GetComponent<CharacterController>();
            wasGrounded = true;
        }

        void Update()
        {
            HandleMovementSounds();
            HandleJumpLandSounds();
        }

        void HandleMovementSounds()
        {
            if (SoundDetectionSystem.Instance == null) return;

            // Get movement state
            bool isMoving = characterController.velocity.magnitude > 0.1f;
            bool isGrounded = controller.Grounded;
            
            if (!isMoving || !isGrounded)
            {
                footstepTimer = 0;
                return;
            }

            // Determine sound type and footstep interval based on movement
            SoundType soundType = SoundType.Silent;
            
            if (IsSneaking())
            {
                soundType = SoundType.VeryQuiet;
                currentFootstepInterval = sneakFootstepInterval;
            }
            else if (IsSprinting())
            {
                soundType = SoundType.Loud;
                currentFootstepInterval = runFootstepInterval;
            }
            else if (isMoving)
            {
                soundType = SoundType.Medium;
                currentFootstepInterval = walkFootstepInterval;
            }

            // Handle footstep timer
            footstepTimer -= Time.deltaTime;
            
            if (footstepTimer <= 0f)
            {
                // Emit sound for detection
                SoundDetectionSystem.Instance.EmitSound(transform.position, soundType, gameObject);
                
                // Play footstep audio
                SoundDetectionSystem.Instance.PlayFootstepSound(soundType);
                
                // Reset timer
                footstepTimer = currentFootstepInterval;
            }
        }

        void HandleJumpLandSounds()
        {
            if (SoundDetectionSystem.Instance == null) return;

            bool isGrounded = controller.Grounded;

            // Detect landing (was in air, now grounded)
            if (!wasGrounded && isGrounded)
            {
                // Emit loud sound for landing
                SoundDetectionSystem.Instance.EmitSound(transform.position, SoundType.Loud, gameObject);
                SoundDetectionSystem.Instance.PlayLandSound();
            }

            wasGrounded = isGrounded;
        }

        // This will be called from ThirdPersonController when jump happens
        public void OnJump()
        {
            if (SoundDetectionSystem.Instance != null)
            {
                // Emit loud sound for jumping
                SoundDetectionSystem.Instance.EmitSound(transform.position, SoundType.Loud, gameObject);
                SoundDetectionSystem.Instance.PlayJumpSound();
            }
        }

        private bool IsSneaking()
        {
            // Access private field through reflection or make it public
            // For now, we'll check if moving slowly
            return characterController.velocity.magnitude < controller.MoveSpeed * 0.6f && 
                   characterController.velocity.magnitude > 0.1f;
        }

        private bool IsSprinting()
        {
            return characterController.velocity.magnitude > controller.MoveSpeed * 1.5f;
        }
    }
}