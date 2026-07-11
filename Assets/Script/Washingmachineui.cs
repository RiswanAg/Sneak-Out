using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI for the Washing Machine distraction.
///
/// Design goals (rewrite):
/// - CONSISTENT every use. Panel visibility is recomputed from the machine's
///   real state every frame (no sticky was/isRunning flags that used to desync
///   after the first cycle and left the timer showing only once).
/// - The countdown / running time is always shown as a SLIDER meter bar.
/// - A light code restyle gives the three panels a consistent modern look.
///
/// The existing scene canvas + references (prompt / interaction / status) are
/// reused so the working draggable slider and layout stay intact.
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

    [Header("Theme")]
    public bool applyRestyle = true;

    // ---- Theme colours ----
    static readonly Color CardColor = new Color(0.07f, 0.09f, 0.11f, 0.97f);
    static readonly Color AccentColor = new Color(0.35f, 0.8f, 1f, 1f);      // laundry blue
    static readonly Color CountdownColor = new Color(0.35f, 0.8f, 1f, 1f);
    static readonly Color RunningColor = new Color(1f, 0.78f, 0.25f, 1f);    // amber
    static readonly Color TextColor = new Color(0.9f, 0.94f, 0.98f, 1f);
    static readonly Color DangerColor = new Color(0.85f, 0.3f, 0.22f, 1f);

    // ---- State ----
    private bool playerNear = false;
    private bool panelOpen = false;
    private float lastTimer;

    // Generated countdown meter (built into the status panel)
    private RectTransform meterFillRect;
    private Image meterFill;

    void Start()
    {
        lastTimer = Mathf.Clamp(defaultTimer, minTimer, maxTimer);

        // Slider setup
        if (timerSlider != null)
        {
            timerSlider.minValue = minTimer;
            timerSlider.maxValue = maxTimer;
            timerSlider.value = lastTimer;
            timerSlider.onValueChanged.RemoveListener(OnSliderChanged);
            timerSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        // Buttons
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartPressed);
            startButton.onClick.AddListener(OnStartPressed);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelPressed);
            cancelButton.onClick.AddListener(OnCancelPressed);
        }

        if (applyRestyle)
            RestyleUI();

        UpdateTimerValueText();
        RefreshVisibility();
    }

    void Update()
    {
        // Open the setup panel
        if (playerNear && !panelOpen && !MachineBusy() && Input.GetKeyDown(interactKey))
            OpenPanel();

        // Close with the close key
        if (panelOpen && Input.GetKeyDown(closeKey))
            ClosePanel();

        // If the machine became busy (e.g. partner started it) while the setup
        // panel is open, drop back to the status view.
        if (panelOpen && MachineBusy())
            ClosePanel();

        RefreshVisibility();
    }

    bool MachineBusy()
    {
        return washingMachine != null && (washingMachine.IsTimerActive || washingMachine.isRunning);
    }

    // ==================== VISIBILITY (recomputed every frame) ====================

    /// <summary>
    /// Single source of truth for what is on screen. Because it derives purely
    /// from the machine's live state + local interaction flags, it behaves
    /// identically on the 1st and the Nth use.
    /// </summary>
    void RefreshVisibility()
    {
        bool counting = washingMachine != null && washingMachine.IsTimerActive && !washingMachine.isRunning;
        bool running = washingMachine != null && washingMachine.isRunning;

        bool showPrompt = playerNear && !panelOpen && !counting && !running;
        if (interactPrompt != null && interactPrompt.activeSelf != showPrompt)
            interactPrompt.SetActive(showPrompt);
        if (showPrompt && promptText != null)
            promptText.text = $"Press [{interactKey}]   ·   Washing Machine";

        bool showSetup = panelOpen && !counting && !running;
        if (interactionPanel != null && interactionPanel.activeSelf != showSetup)
            interactionPanel.SetActive(showSetup);

        bool showStatus = playerNear && (counting || running);
        if (statusPanel != null && statusPanel.activeSelf != showStatus)
            statusPanel.SetActive(showStatus);
        if (showStatus)
            UpdateStatus(counting, running);
    }

    void UpdateStatus(bool counting, bool running)
    {
        float fraction;
        Color color;
        string label;

        if (counting)
        {
            float t = washingMachine.CurrentTimer;
            fraction = Mathf.Clamp01(t / washingMachine.CurrentTimerDuration);
            color = CountdownColor;
            label = $"Starting in {Mathf.CeilToInt(t)}s — move away!";
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        }
        else // running
        {
            float t = washingMachine.RunTimeRemaining;
            fraction = Mathf.Clamp01(t / Mathf.Max(0.01f, washingMachine.runDuration));
            color = RunningColor;
            label = $"Running · {Mathf.CeilToInt(t)}s of noise left";
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        }

        if (statusText != null) statusText.text = label;
        SetMeter(fraction, color);
    }

    void SetMeter(float fraction, Color color)
    {
        if (meterFillRect == null) return;
        fraction = Mathf.Clamp01(fraction);
        meterFillRect.anchorMin = new Vector2(0f, 0f);
        meterFillRect.anchorMax = new Vector2(fraction, 1f);
        meterFillRect.offsetMin = Vector2.zero;
        meterFillRect.offsetMax = Vector2.zero;
        if (meterFill != null) meterFill.color = color;
    }

    // ==================== PANEL CONTROL ====================

    void OpenPanel()
    {
        panelOpen = true;
        if (timerSlider != null)
            timerSlider.value = Mathf.Clamp(lastTimer, minTimer, maxTimer);
        UpdateTimerValueText();
        CursorManager.SetFree();
        GameLog.Log("[WashingMachineUI] Panel opened");
    }

    void ClosePanel()
    {
        if (!panelOpen) return;
        panelOpen = false;
        CursorManager.SetLocked();
        GameLog.Log("[WashingMachineUI] Panel closed");
    }

    // ==================== BUTTON / SLIDER CALLBACKS ====================

    void OnSliderChanged(float value)
    {
        lastTimer = value;
        UpdateTimerValueText();
    }

    void UpdateTimerValueText()
    {
        if (timerValueText != null && timerSlider != null)
            timerValueText.text = $"{Mathf.RoundToInt(timerSlider.value)}s";
    }

    void OnStartPressed()
    {
        if (washingMachine == null)
        {
            Debug.LogError("[WashingMachineUI] WashingMachine reference not set!");
            return;
        }

        float timerValue = timerSlider != null ? timerSlider.value : defaultTimer;
        lastTimer = timerValue;

        GameLog.Log($"[WashingMachineUI] Starting timer: {timerValue}s");

        // SetTimer is networked; it returns false only if already busy. Either
        // way we close the setup panel - RefreshVisibility shows the correct
        // status next frame from the machine's real state.
        washingMachine.SetTimer(timerValue);
        ClosePanel();
    }

    void OnCancelPressed()
    {
        if (washingMachine != null)
            washingMachine.CancelTimer();

        GameLog.Log("[WashingMachineUI] Timer cancelled");
    }

    // ==================== PLAYER DETECTION (called by WashingMachine) ====================

    public void OnPlayerEnter()
    {
        playerNear = true;
        GameLog.Log("[WashingMachineUI] Player entered range");
    }

    public void OnPlayerExit()
    {
        playerNear = false;
        ClosePanel();
        GameLog.Log("[WashingMachineUI] Player exited range");
    }

    public void SetWashingMachine(WashingMachine machine)
    {
        washingMachine = machine;
    }

    // ==================== RESTYLE (redesign) ====================

    void RestyleUI()
    {
        StyleCard(interactionPanel, AccentColor);
        StyleCard(statusPanel, RunningColor);
        StylePromptPill(interactPrompt);

        StyleButton(startButton, AccentColor, Color.black);
        StyleButton(closeButton, new Color(0.15f, 0.17f, 0.2f, 1f), TextColor);
        StyleButton(cancelButton, DangerColor, Color.white);

        if (timerValueText != null)
        {
            timerValueText.color = Color.white;
            timerValueText.fontStyle = FontStyles.Bold;
        }
        if (statusText != null)
        {
            statusText.color = TextColor;
            statusText.fontStyle = FontStyles.Bold;
        }
        if (promptText != null)
            promptText.color = TextColor;

        BuildStatusMeter();
    }

    void StyleCard(GameObject panel, Color accent)
    {
        if (panel == null) return;

        Image bg = panel.GetComponent<Image>();
        if (bg != null) bg.color = CardColor;

        // Top accent bar (only add once)
        if (panel.transform.Find("__AccentBar") == null)
        {
            GameObject bar = NewUI("__AccentBar", panel.transform);
            RectTransform rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -8f);
            rt.offsetMax = Vector2.zero;
            Image img = bar.AddComponent<Image>();
            img.color = accent;
            img.raycastTarget = false;
        }
    }

    void StylePromptPill(GameObject prompt)
    {
        if (prompt == null) return;
        Image bg = prompt.GetComponent<Image>();
        if (bg != null) bg.color = new Color(0.05f, 0.07f, 0.09f, 0.92f);
    }

    void StyleButton(Button button, Color normal, Color textColor)
    {
        if (button == null) return;

        Image img = button.GetComponent<Image>();
        if (img != null) img.color = normal;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = textColor;
            label.fontStyle = FontStyles.Bold;
        }

        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.selectedColor = Color.white;
        button.colors = cb;
        button.transition = Selectable.Transition.ColorTint;
    }

    /// <summary>Adds a horizontal countdown meter (bg + fill) to the status panel.</summary>
    void BuildStatusMeter()
    {
        if (statusPanel == null) return;
        if (statusPanel.transform.Find("__CountdownMeter") != null) return;

        GameObject meter = NewUI("__CountdownMeter", statusPanel.transform);
        RectTransform meterRT = (RectTransform)meter.transform;
        meterRT.anchorMin = new Vector2(0.06f, 0f);
        meterRT.anchorMax = new Vector2(0.94f, 0f);
        meterRT.pivot = new Vector2(0.5f, 0f);
        meterRT.anchoredPosition = new Vector2(0f, 22f);
        meterRT.sizeDelta = new Vector2(0f, 16f);
        Image meterBg = meter.AddComponent<Image>();
        meterBg.color = new Color(0.04f, 0.05f, 0.06f, 0.95f);
        meterBg.raycastTarget = false;

        GameObject fill = NewUI("Fill", meter.transform);
        meterFillRect = (RectTransform)fill.transform;
        meterFillRect.anchorMin = new Vector2(0f, 0f);
        meterFillRect.anchorMax = new Vector2(1f, 1f);
        meterFillRect.offsetMin = Vector2.zero;
        meterFillRect.offsetMax = Vector2.zero;
        meterFill = fill.AddComponent<Image>();
        meterFill.color = CountdownColor;
        meterFill.raycastTarget = false;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
