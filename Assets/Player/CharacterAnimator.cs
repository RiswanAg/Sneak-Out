using UnityEngine;

namespace CustomCharacterController
{
    [RequireComponent(typeof(CharacterMotor))]
    public class CharacterAnimator : MonoBehaviour
    {
        protected CharacterMotor motor;
        protected Animator animator;
        
        // Animator parameter hashes for better performance
        private int isWalkHash;
        private int speedHash;
        private int isSneakHash;
        private int isRunningHash;
        private int isGroundedHash;
        private int jumpHash;
        
        // Movement variables
        protected Vector3 moveDirection;
        protected Vector2 input;
        protected Vector3 inputSmooth;
        protected float movementSpeed;
        
        public virtual void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            animator = GetComponent<Animator>();
            
            // Cache animator parameter hashes
            isWalkHash = Animator.StringToHash("isWalk");
            speedHash = Animator.StringToHash("Speed");
            isSneakHash = Animator.StringToHash("isSneak");
            isRunningHash = Animator.StringToHash("isRunning");
            isGroundedHash = Animator.StringToHash("isGrounded");
            jumpHash = Animator.StringToHash("Jump");
        }
        
        public virtual void Update()
        {
            UpdateAnimator();
        }
        
        protected virtual void UpdateAnimator()
        {
            if (animator == null) return;
            
            // Update animator parameters
            animator.SetBool(isWalkHash, input.magnitude > 0.1f);
            animator.SetFloat(speedHash, movementSpeed);
            animator.SetBool(isSneakHash, motor.isSneaking);
            animator.SetBool(isRunningHash, motor.isSprinting);
            animator.SetBool(isGroundedHash, motor.isGrounded);
        }
        
        public virtual void TriggerJump()
        {
            if (animator != null)
            {
                animator.SetTrigger(jumpHash);
            }
        }
        
        public void SetInput(Vector2 newInput)
        {
            input = newInput;
        }
        
        public void SetInputSmooth(Vector3 smooth)
        {
            inputSmooth = smooth;
        }
        
        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction;
        }
        
        public void SetMovementSpeed(float speed)
        {
            movementSpeed = speed;
        }
        
        // Handle root motion if enabled
        void OnAnimatorMove()
        {
            if (animator == null) return;
            
            // Apply root motion delta to character
            Vector3 rootMotionDelta = animator.deltaPosition;
            
            // Only apply horizontal movement from root motion
            rootMotionDelta.y = 0;
            
            if (rootMotionDelta.magnitude > 0.001f)
            {
                motor.ApplyMovement(rootMotionDelta / Time.deltaTime);
            }
            else
            {
                // If no root motion, apply movement from motor
                motor.ApplyMovement(motor.characterController.velocity);
            }
        }
    }
}