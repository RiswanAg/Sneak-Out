using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

namespace StarterAssets
{
    /// <summary>
    /// Displays stamina bar for the LOCAL player.
    /// FIXED: Now waits for player to spawn in multiplayer.
    /// </summary>
    public class StaminaUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the player's ThirdPersonController (auto-finds local player if empty)")]
        public ThirdPersonController playerController;

        [Tooltip("The stamina bar slider")]
        public Slider staminaSlider;

        [Tooltip("Optional: Image component for the fill (to change color)")]
        public Image staminaFillImage;

        [Header("Visual Settings")]
        [Tooltip("Color when stamina is full")]
        public Color fullStaminaColor = new Color(0.2f, 1f, 0.2f); // Green

        [Tooltip("Color when stamina is low")]
        public Color lowStaminaColor = new Color(1f, 0.2f, 0.2f); // Red

        [Tooltip("Stamina percentage considered 'low' (0-1)")]
        [Range(0f, 1f)]
        public float lowStaminaThreshold = 0.3f;

        [Header("Auto-Hide Settings")]
        [Tooltip("Hide stamina bar when full")]
        public bool autoHideWhenFull = true;

        [Tooltip("Delay before hiding when stamina is full (seconds)")]
        public float hideDelay = 2f;

        private CanvasGroup canvasGroup;
        private float hideTimer;
        private bool wasFull;
        private bool isInitialized = false;
        private float searchTimer = 0f;
        private float searchInterval = 0.5f; // Search every 0.5 seconds

        void Start()
        {
            // Get or add CanvasGroup for fade effects
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && autoHideWhenFull)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // Start hidden until we find player
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            wasFull = true;
            
            // Try to find player immediately
            TryFindLocalPlayer();
        }

        void Update()
        {
            // ✅ If not initialized, keep searching for local player
            if (!isInitialized)
            {
                searchTimer += Time.deltaTime;
                if (searchTimer >= searchInterval)
                {
                    searchTimer = 0f;
                    TryFindLocalPlayer();
                }
                return;
            }
            
            // Check if player was destroyed (scene reload, etc.)
            if (playerController == null)
            {
                isInitialized = false;
                return;
            }

            // Update slider value
            if (staminaSlider != null)
            {
                staminaSlider.value = playerController.CurrentStamina;
            }

            // Calculate stamina percentage
            float staminaPercent = playerController.CurrentStamina / playerController.MaxStamina;

            // Update color based on stamina level
            if (staminaFillImage != null)
            {
                staminaFillImage.color = Color.Lerp(lowStaminaColor, fullStaminaColor, 
                    Mathf.InverseLerp(0f, lowStaminaThreshold, staminaPercent));
            }

            // Handle auto-hide
            if (autoHideWhenFull && canvasGroup != null)
            {
                bool isFull = staminaPercent >= 0.99f;

                if (isFull)
                {
                    if (!wasFull)
                    {
                        // Just became full, start hide timer
                        hideTimer = hideDelay;
                        wasFull = true;
                    }

                    // Count down and fade out
                    if (hideTimer > 0)
                    {
                        hideTimer -= Time.deltaTime;
                    }
                    else
                    {
                        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * 5f);
                    }
                }
                else
                {
                    // Not full, show immediately
                    wasFull = false;
                    hideTimer = hideDelay;
                    canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * 10f);
                }
            }
        }
        
        /// <summary>
        /// ✅ FIXED: Find the LOCAL player's ThirdPersonController
        /// </summary>
        void TryFindLocalPlayer()
        {
            // If already assigned in inspector, use that
            if (playerController != null)
            {
                Initialize();
                return;
            }
            
            // ✅ Find LOCAL player only (not remote players)
            ThirdPersonController[] allControllers = FindObjectsOfType<ThirdPersonController>();
            
            foreach (ThirdPersonController controller in allControllers)
            {
                // Check if this is the local player
                PhotonView pv = controller.GetComponent<PhotonView>();
                
                if (pv != null && pv.IsMine)
                {
                    playerController = controller;
                    Debug.Log($"[StaminaUI] ✅ Found local player: {controller.gameObject.name}");
                    Initialize();
                    return;
                }
                
                // Fallback for offline/single player mode
                if (pv == null && !PhotonNetwork.IsConnected)
                {
                    playerController = controller;
                    Debug.Log($"[StaminaUI] ✅ Found player (offline mode): {controller.gameObject.name}");
                    Initialize();
                    return;
                }
            }
            
            // Still searching...
            // Don't log error - player might not have spawned yet
        }
        
        void Initialize()
        {
            if (playerController == null) return;
            
            // Initialize slider
            if (staminaSlider != null)
            {
                staminaSlider.maxValue = playerController.MaxStamina;
                staminaSlider.value = playerController.CurrentStamina;
            }
            
            // Show the UI
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            isInitialized = true;
            Debug.Log("[StaminaUI] ✅ Initialized successfully");
        }

        // Optional: Call this to force show the stamina bar
        public void ForceShow()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                hideTimer = hideDelay;
            }
        }

        // Optional: Call this to force hide the stamina bar
        public void ForceHide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        /// <summary>
        /// Call this to manually set the player (e.g., from PlayerSpawnerPun)
        /// </summary>
        public void SetPlayer(ThirdPersonController controller)
        {
            playerController = controller;
            Initialize();
        }
    }
}