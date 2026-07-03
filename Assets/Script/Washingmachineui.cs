using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI for Washing Machine interaction
/// Shows timer slider and start/cancel buttons
/// </summary>
public class WashingMachineUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The washing machine this UI controls")]
    public WashingMachine washingMachine;
    
    [Header("UI Panels")]
    [Tooltip("Simple prompt shown when player is near (Press E to interact)")]
    public GameObject interactPrompt;
    
    [Tooltip("Full interaction panel with timer slider")]
    public GameObject interactionPanel;
    
    [Tooltip("Status panel shown when timer is counting down or running")]
    public GameObject statusPanel;
    
    [Header("Interact Prompt Elements")]
    public TMP_Text promptText;
    
    [Header("Interaction Panel Elements")]
    [Tooltip("Slider to set timer delay")]
    public Slider timerSlider;
    
    [Tooltip("Text showing current timer value")]
    public TMP_Text timerValueText;
    
    [Tooltip("Button to start the timer")]
    public Button startButton;
    
    [Tooltip("Button to close the panel")]
    public Button closeButton;
    
    [Header("Status Panel Elements")]
    [Tooltip("Text showing countdown or running status")]
    public TMP_Text statusText;
    
    [Tooltip("Button to cancel timer (only when counting down)")]
    public Button cancelButton;
    
    [Header("Settings")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode closeKey = KeyCode.Escape;
    
    [Header("Timer Range")]
    public float minTimer = 3f;
    public float maxTimer = 30f;
    public float defaultTimer = 5f;
    
    // State
    private bool playerNear = false;
    private bool panelOpen = false;
    private bool isCountingDown = false;
    private bool isRunning = false;
    private float countdownTime = 0f;
    
    void Start()
    {
        // Setup slider
        if (timerSlider != null)
        {
            timerSlider.minValue = minTimer;
            timerSlider.maxValue = maxTimer;
            timerSlider.value = defaultTimer;
            timerSlider.onValueChanged.AddListener(OnSliderChanged);
        }
        
        // Setup buttons
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartPressed);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelPressed);
        }
        
        // Initial state - hide all
        HideAllUI();
        
        // Update timer text
        UpdateTimerValueText();
    }
    
    void Update()
    {
        // Sync state with washing machine
        if (washingMachine != null)
        {
            isRunning = washingMachine.isRunning;
        }
        
        // Handle input
        if (playerNear && !panelOpen && !isCountingDown && !isRunning)
        {
            if (Input.GetKeyDown(interactKey))
            {
                OpenPanel();
            }
        }
        
        if (panelOpen && Input.GetKeyDown(closeKey))
        {
            ClosePanel();
        }
        
        // Update countdown
        if (isCountingDown)
        {
            countdownTime -= Time.deltaTime;
            
            if (countdownTime <= 0f)
            {
                // Timer finished, machine starts
                isCountingDown = false;
                isRunning = true;
                UpdateStatusPanel();
            }
            else
            {
                UpdateStatusPanel();
            }
        }
        
        // Check if machine stopped
        if (isRunning && washingMachine != null && !washingMachine.isRunning)
        {
            isRunning = false;
            HideStatusPanel();
        }
        
        // Update prompt visibility
        UpdatePromptVisibility();
    }
    
    // ==================== UI VISIBILITY ====================
    
    void HideAllUI()
    {
        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (interactionPanel != null) interactionPanel.SetActive(false);
        if (statusPanel != null) statusPanel.SetActive(false);
    }
    
    void UpdatePromptVisibility()
    {
        if (interactPrompt == null) return;
        
        // Show prompt only when: player near, panel closed, not counting down, not running
        bool showPrompt = playerNear && !panelOpen && !isCountingDown && !isRunning;
        interactPrompt.SetActive(showPrompt);
        
        if (showPrompt && promptText != null)
        {
            promptText.text = $"Press [{interactKey}] to use Washing Machine";
        }
    }
    
    /// <summary>
    /// Show status when machine is running (player walks back to it)
    /// </summary>
    void UpdateRunningStatus()
    {
        if (!playerNear) return;
        
        // If machine is running and player is near, show running status
        if (isRunning && !panelOpen)
        {
            ShowStatusPanel();
            if (statusText != null)
            {
                statusText.text = "🌀 RUNNING...";
            }
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(false); // Can't cancel while running
            }
        }
    }
    
    void OpenPanel()
    {
        panelOpen = true;
        
        if (interactionPanel != null)
            interactionPanel.SetActive(true);
        
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
        
        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Pause player movement (optional)
        // Time.timeScale = 0f; // Uncomment if you want to pause game
        
        Debug.Log("[WashingMachineUI] Panel opened");
    }
    
    void ClosePanel()
    {
        panelOpen = false;
        
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
        
        // Hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Resume game
        // Time.timeScale = 1f;
        
        Debug.Log("[WashingMachineUI] Panel closed");
    }
    
    void ShowStatusPanel()
    {
        if (statusPanel != null)
            statusPanel.SetActive(true);
        
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }
    
    void HideStatusPanel()
    {
        if (statusPanel != null)
            statusPanel.SetActive(false);
    }
    
    void UpdateStatusPanel()
    {
        ShowStatusPanel();
        
        if (statusText != null)
        {
            if (isCountingDown)
            {
                statusText.text = $"Starting in: {Mathf.CeilToInt(countdownTime)}s";
            }
            else if (isRunning)
            {
                statusText.text = "🌀 RUNNING...";
            }
        }
        
        // Show cancel button only during countdown
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(isCountingDown);
        }
    }
    
    // ==================== BUTTON CALLBACKS ====================
    
    void OnSliderChanged(float value)
    {
        UpdateTimerValueText();
    }
    
    void UpdateTimerValueText()
    {
        if (timerValueText != null && timerSlider != null)
        {
            timerValueText.text = $"{Mathf.RoundToInt(timerSlider.value)} seconds";
        }
    }
    
    void OnStartPressed()
    {
        if (washingMachine == null)
        {
            Debug.LogError("[WashingMachineUI] WashingMachine reference not set!");
            return;
        }
        
        float timerValue = timerSlider != null ? timerSlider.value : defaultTimer;
        
        Debug.Log($"[WashingMachineUI] Starting timer: {timerValue}s");
        
        // Start the washing machine timer
        washingMachine.SetTimer(timerValue);
        
        // Update UI state
        isCountingDown = true;
        countdownTime = timerValue;
        
        // Close interaction panel, show status
        ClosePanel();
        UpdateStatusPanel();
    }
    
    void OnCancelPressed()
    {
        if (washingMachine != null)
        {
            washingMachine.CancelTimer();
        }
        
        isCountingDown = false;
        countdownTime = 0f;
        
        HideStatusPanel();
        
        Debug.Log("[WashingMachineUI] Timer cancelled");
    }
    
    // ==================== PLAYER DETECTION ====================
    
    /// <summary>
    /// Call this from WashingMachine when player enters trigger
    /// </summary>
    public void OnPlayerEnter()
    {
        playerNear = true;
        Debug.Log("[WashingMachineUI] Player entered range");
        
        // If machine is already running, show status
        if (washingMachine != null && washingMachine.isRunning)
        {
            isRunning = true;
            ShowStatusPanel();
            if (statusText != null)
            {
                statusText.text = "🌀 RUNNING...";
            }
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Call this from WashingMachine when player exits trigger
    /// </summary>
    public void OnPlayerExit()
    {
        playerNear = false;
        ClosePanel();
        HideStatusPanel(); // Also hide status when leaving
        Debug.Log("[WashingMachineUI] Player exited range");
    }
    
    // ==================== PUBLIC METHODS ====================
    
    /// <summary>
    /// Set the washing machine reference at runtime
    /// </summary>
    public void SetWashingMachine(WashingMachine machine)
    {
        washingMachine = machine;
    }
}