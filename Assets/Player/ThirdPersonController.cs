using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomCharacterController
{
    public class ThirdPersonController : CharacterAnimator
    {
        [Header("Movement Settings")]
        public float movementSmoothness = 10f;
        public bool rotateWithCamera = true;
        
        [Header("Camera Reference")]
        public Transform cameraTransform;
        
        // Input values
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool sprintInput;
        private bool sneakInput;
        private bool jumpInput;
        
        // Camera reference
        private ThirdPersonCamera thirdPersonCamera;
        
        public override void Awake()
        {
            base.Awake();
            
            // Find camera if not assigned
            if (cameraTransform == null)
            {
                thirdPersonCamera = FindObjectOfType<ThirdPersonCamera>();
                if (thirdPersonCamera != null)
                {
                    cameraTransform = thirdPersonCamera.transform;
                }
            }
            else
            {
                thirdPersonCamera = cameraTransform.GetComponent<ThirdPersonCamera>();
            }
        }
        
        public override void Update()
        {
            base.Update();
            
            HandleInput();
            UpdateMoveDirection();
            ControlLocomotion();
            ControlRotation();
        }
        
        void HandleInput()
        {
            // Handle sprint
            motor.isSprinting = sprintInput && moveInput.magnitude > 0.1f && motor.currentStamina > 0 && motor.isGrounded;
            
            // Handle sneak
            motor.isSneaking = sneakInput && moveInput.magnitude > 0.1f;
            
            // Handle jump
            if (jumpInput && motor.isGrounded)
            {
                motor.Jump();
                TriggerJump();
                jumpInput = false; // Reset jump input
            }
            
            // Send look input to camera
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.RotateCamera(lookInput.x, lookInput.y);
            }
        }
        
        void UpdateMoveDirection()
        {
            if (moveInput.magnitude <= 0.01f)
            {
                inputSmooth = Vector3.Lerp(inputSmooth, Vector3.zero, movementSmoothness * Time.deltaTime);
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, movementSmoothness * Time.deltaTime);
                SetInputSmooth(inputSmooth);
                SetMoveDirection(moveDirection);
                return;
            }
            
            // Smooth input
            input = moveInput;
            SetInput(input);
            
            Vector3 targetInput = new Vector3(moveInput.x, 0, moveInput.y);
            inputSmooth = Vector3.Lerp(inputSmooth, targetInput, movementSmoothness * Time.deltaTime);
            SetInputSmooth(inputSmooth);
            
            // Calculate move direction relative to camera
            if (cameraTransform != null && rotateWithCamera)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                
                // Flatten directions
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
                
                moveDirection = (inputSmooth.x * right) + (inputSmooth.z * forward);
            }
            else
            {
                moveDirection = new Vector3(inputSmooth.x, 0, inputSmooth.z);
            }
            
            SetMoveDirection(moveDirection);
        }
        
        void ControlLocomotion()
        {
            float currentSpeed = motor.GetCurrentSpeed();
            SetMovementSpeed(inputSmooth.magnitude * currentSpeed);
            
            // Apply movement through motor
            motor.Move(moveDirection, currentSpeed);
        }
        
        void ControlRotation()
        {
            if (moveDirection.magnitude > 0.1f)
            {
                motor.RotateToDirection(moveDirection);
            }
        }
        
        #region Input System Callbacks

        // Movement - expects Vector2
        public void OnMove(Vector2 value)
        {
            moveInput = value;
        }

        // Look - expects Vector2
        public void OnLook(Vector2 value)
        {
            lookInput = value;
        }

        // Sprint - expects bool (pressed / released)
        public void OnSprint(bool value)
        {
            sprintInput = value;
        }

        // Sneak - expects bool
        public void OnSneak(bool value)
        {
            sneakInput = value;
        }

        // Jump - no value needed
        public void OnJump()
        {
            jumpInput = true;
        }

        #endregion


    }
}