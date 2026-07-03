using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// ButtonModule.cs - Button press/hold puzzle (FIXED VERSION v3)
/// 
/// FIXES v3:
/// - ColorStrip no longer auto-unchecks in Inspector
/// - Uses CanvasGroup for show/hide instead of SetActive
/// - Properly maintains visibility state
/// </summary>
public class ButtonModule : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI References")]
    public GameObject modulePanel;
    public Button mainButton;
    public Image buttonImage;
    public TMP_Text buttonLabel;
    public Image colorStrip;
    public TMP_Text instructionText;
    
    [Header("Button Colors")]
    public Color blueButton = new Color(0.2f, 0.4f, 1f);
    public Color redButton = new Color(1f, 0.2f, 0.2f);
    public Color yellowButton = new Color(1f, 0.9f, 0.2f);
    public Color whiteButton = Color.white;
    
    [Header("Strip Colors")]
    public Color blueStrip = new Color(0.2f, 0.4f, 1f);
    public Color whiteStrip = Color.white;
    public Color yellowStrip = new Color(1f, 0.9f, 0.2f);
    public Color redStrip = new Color(1f, 0.2f, 0.2f);
    
    [Header("Settings")]
    public float holdThreshold = 0.5f;
    public float maxHoldTime = 10f;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pressSound;
    public AudioClip holdStartSound;
    public AudioClip releaseSound;
    public AudioClip wrongSound;
    
    public enum ButtonColor { Blue, Red, Yellow, White }
    public enum StripColor { Blue, White, Yellow, Red }
    
    private ButtonColor currentButtonColor;
    private string currentLabel;
    private StripColor currentStripColor;
    private bool shouldHold;
    private int releaseOnDigit;
    
    private bool isActive = false;
    private bool isComplete = false;
    private bool isHolding = false;
    private float holdStartTime;
    private bool stripShown = false;
    
    private string[] possibleLabels = { "SAHKAN", "BATAL", "TAHAN", "TEKAN" };
    
    // ✅ NEW: CanvasGroup for show/hide instead of SetActive
    private CanvasGroup stripCanvasGroup;
    
    void Start()
    {
        SetupColorStrip();
    }
    
    void SetupColorStrip()
    {
        if (colorStrip == null)
        {
            Debug.LogError("<color=red>[ButtonModule] ColorStrip NOT assigned!</color>");
            return;
        }
        
        // ✅ Add CanvasGroup if it doesn't exist
        stripCanvasGroup = colorStrip.GetComponent<CanvasGroup>();
        if (stripCanvasGroup == null)
        {
            stripCanvasGroup = colorStrip.gameObject.AddComponent<CanvasGroup>();
            Debug.Log("<color=cyan>[ButtonModule] Added CanvasGroup to ColorStrip</color>");
        }
        
        // ✅ Hide using CanvasGroup (GameObject stays active!)
        HideColorStrip();
        
        Debug.Log($"<color=cyan>[ButtonModule] ColorStrip setup complete and hidden</color>");
    }
    
    void HideColorStrip()
    {
        if (stripCanvasGroup != null)
        {
            stripCanvasGroup.alpha = 0f;
            stripCanvasGroup.interactable = false;
            stripCanvasGroup.blocksRaycasts = false;
        }
    }
    
    void ShowColorStrip()
    {
        if (colorStrip == null)
        {
            Debug.LogError("<color=red>[ButtonModule] Cannot show strip - NULL!</color>");
            return;
        }
        
        if (stripCanvasGroup == null)
        {
            Debug.LogError("<color=red>[ButtonModule] CanvasGroup missing!</color>");
            return;
        }
        
        // ✅ Show using CanvasGroup
        stripCanvasGroup.alpha = 1f;
        stripCanvasGroup.interactable = true;
        stripCanvasGroup.blocksRaycasts = true;
        
        // Set color
        Color c = GetUnityStripColor(currentStripColor);
        c.a = 1f;
        colorStrip.color = c;
        
        // Force canvas update
        Canvas.ForceUpdateCanvases();
        
        Debug.Log($"<color=lime>[ButtonModule] ★ STRIP SHOWN! Color: {currentStripColor}</color>");
        Debug.Log($"<color=lime>[ButtonModule] Strip alpha: {stripCanvasGroup.alpha}</color>");
        Debug.Log($"<color=lime>[ButtonModule] Strip color: {colorStrip.color}</color>");
        
        if (audioSource != null && holdStartSound != null)
            audioSource.PlayOneShot(holdStartSound);
        
        if (instructionText != null)
            instructionText.text = $"Lepas bila timer ada angka {releaseOnDigit}";
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
        if (modulePanel != null)
            modulePanel.SetActive(active);
    }
    
    public void Initialize()
    {
        isComplete = false;
        isHolding = false;
        stripShown = false;
        
        ButtonColor[] colors = { ButtonColor.Blue, ButtonColor.Red, ButtonColor.Yellow, ButtonColor.White };
        currentButtonColor = colors[Random.Range(0, colors.Length)];
        
        currentLabel = possibleLabels[Random.Range(0, possibleLabels.Length)];
        
        StripColor[] strips = { StripColor.Blue, StripColor.White, StripColor.Yellow, StripColor.Red };
        currentStripColor = strips[Random.Range(0, strips.Length)];
        
        shouldHold = CalculateShouldHold();
        releaseOnDigit = CalculateReleaseDigit();
        
        SetupButtonUI();
        
        Debug.Log($"<color=cyan>=== BUTTON MODULE ===</color>");
        Debug.Log($"<color=cyan>Button: {currentButtonColor}, Label: {currentLabel}</color>");
        Debug.Log($"<color=cyan>Strip: {currentStripColor}</color>");
        Debug.Log($"<color=lime>Should Hold: {shouldHold}, Release on: {releaseOnDigit}</color>");
    }
    
    void SetupButtonUI()
    {
        if (buttonImage != null)
            buttonImage.color = GetUnityButtonColor(currentButtonColor);
        
        if (buttonLabel != null)
        {
            buttonLabel.text = currentLabel;
            buttonLabel.color = (currentButtonColor == ButtonColor.White || currentButtonColor == ButtonColor.Yellow) 
                ? Color.black : Color.white;
        }
        
        // Setup ColorStrip
        if (colorStrip != null)
        {
            colorStrip.color = GetUnityStripColor(currentStripColor);
            HideColorStrip();
            
            Debug.Log($"<color=yellow>[ButtonModule] Strip color set to: {currentStripColor} ({colorStrip.color})</color>");
        }
        
        if (instructionText != null)
            instructionText.text = "";
    }
    
    Color GetUnityButtonColor(ButtonColor color)
    {
        switch (color)
        {
            case ButtonColor.Blue: return blueButton;
            case ButtonColor.Red: return redButton;
            case ButtonColor.Yellow: return yellowButton;
            case ButtonColor.White: return whiteButton;
            default: return Color.gray;
        }
    }
    
    Color GetUnityStripColor(StripColor color)
    {
        switch (color)
        {
            case StripColor.Blue: return blueStrip;
            case StripColor.White: return whiteStrip;
            case StripColor.Yellow: return yellowStrip;
            case StripColor.Red: return redStrip;
            default: return Color.gray;
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActive || isComplete) return;
        
        Debug.Log("<color=yellow>[ButtonModule] Button DOWN</color>");
        
        isHolding = true;
        holdStartTime = Time.time;
        stripShown = false;
        
        if (audioSource != null && pressSound != null)
            audioSource.PlayOneShot(pressSound);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isActive || isComplete || !isHolding) return;
        
        float holdDuration = Time.time - holdStartTime;
        bool wasHold = holdDuration >= holdThreshold;
        
        Debug.Log($"<color=yellow>[ButtonModule] Button UP - Duration: {holdDuration:F2}s</color>");
        
        isHolding = false;
        
        // Hide strip
        HideColorStrip();
        
        if (audioSource != null && releaseSound != null)
            audioSource.PlayOneShot(releaseSound);
        
        CheckResult(wasHold);
    }
    
    void Update()
    {
        if (!isActive || isComplete) return;
        
        if (isHolding)
        {
            float holdDuration = Time.time - holdStartTime;
            
            // Show strip after threshold
            if (holdDuration >= holdThreshold && !stripShown)
            {
                stripShown = true;
                ShowColorStrip();
            }
            
            // Auto-release after max time
            if (holdDuration >= maxHoldTime)
            {
                Debug.Log("<color=red>[ButtonModule] Held too long!</color>");
                isHolding = false;
                
                HideColorStrip();
                
                if (ControlPanelManager.Instance != null)
                    ControlPanelManager.Instance.AddStrike("Tahan terlalu lama!");
            }
        }
    }
    
    void CheckResult(bool playerHeld)
    {
        bool correct = false;
        
        if (shouldHold)
        {
            if (playerHeld)
                correct = CheckReleaseTime();
            else
                Debug.Log("<color=red>Should HOLD but TAPPED!</color>");
        }
        else
        {
            if (!playerHeld)
            {
                correct = true;
                Debug.Log("<color=lime>Correct TAP!</color>");
            }
            else
                Debug.Log("<color=red>Should TAP but HELD!</color>");
        }
        
        if (correct)
        {
            Debug.Log("<color=lime>✓ BUTTON MODULE CORRECT!</color>");
            isComplete = true;
            
            if (ControlPanelManager.Instance != null)
                ControlPanelManager.Instance.ModuleComplete(1);
        }
        else
        {
            Debug.Log("<color=red>✗ BUTTON MODULE WRONG!</color>");
            
            if (audioSource != null && wrongSound != null)
                audioSource.PlayOneShot(wrongSound);
            
            if (ControlPanelManager.Instance != null)
                ControlPanelManager.Instance.AddStrike("Butang salah!");
            
            if (instructionText != null)
                instructionText.text = "Cuba lagi!";
        }
    }
    
    bool CheckReleaseTime()
    {
        if (ControlPanelManager.Instance == null) return false;
        
        float currentTime = ControlPanelManager.Instance.GetCurrentTime();
        int seconds = Mathf.FloorToInt(currentTime);
        string timeStr = seconds.ToString();
        
        bool hasDigit = timeStr.Contains(releaseOnDigit.ToString());
        
        Debug.Log($"<color=cyan>Timer: {seconds}, Need: {releaseOnDigit}, Found: {hasDigit}</color>");
        
        return hasDigit;
    }
    
    bool CalculateShouldHold()
    {
        if (currentButtonColor == ButtonColor.Blue && currentLabel == "BATAL")
            return true;
        if (currentButtonColor == ButtonColor.Red && currentLabel == "TAHAN")
            return false;
        if (currentButtonColor == ButtonColor.Yellow)
            return true;
        if (currentButtonColor == ButtonColor.White && currentLabel == "SAHKAN")
            return false;
        return true;
    }
    
    int CalculateReleaseDigit()
    {
        switch (currentStripColor)
        {
            case StripColor.Blue: return 4;
            case StripColor.White: return 1;
            case StripColor.Yellow: return 5;
            default: return 1;
        }
    }
    
    public ButtonColor GetButtonColor() => currentButtonColor;
    public string GetLabel() => currentLabel;
    public StripColor GetStripColor() => currentStripColor;
    public bool GetShouldHold() => shouldHold;
}