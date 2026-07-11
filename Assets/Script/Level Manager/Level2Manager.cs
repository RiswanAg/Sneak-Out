using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Level 2 Manager - Team-based game over
/// If ONE player is caught by Cikgu, BOTH players lose!
/// Following Level1Manager restart pattern
/// </summary>
public class Level2Manager : MonoBehaviourPunCallbacks
{
    public static Level2Manager Instance { get; private set; }
    
    [Header("=== LEVEL 2 SETTINGS ===")]
    [Tooltip("Team game over - if one player caught, both lose")]
    public bool teamGameOver = true;
    
    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text gameOverSubtext;
    
    [Header("Buttons")]
    public Button retryButton;
    public Button mainMenuButton;
    
    [Header("Win Condition")]
    public Transform winCheckpoint;
    public float checkpointRadius = 3f;
    public int requiredPlayersAtCheckpoint = 2;
    
    [Header("Victory UI")]
    public GameObject victoryPanel;
    public TMP_Text victoryText;
    
    [Header("Cutscene & Next Level")]
    public GameObject cutsceneObject;
    public string nextLevelScene = SceneNames.Level2VictoryCutscene;
    public float transitionDelay = 3f;
    public bool isFinalLevel = false;

    [Header("Scene Names")]
    public string mainMenuScene = SceneNames.MainMenu;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip gameOverSound;
    public AudioClip victorySound;
    public AudioClip caughtSound;
    
    [Header("Cikgu Reference")]
    [Tooltip("Reference to Cikgu NPC (auto-finds if empty)")]
    public CikguNPC cikguNPC;

    [Header("Objective HUD")]
    public bool autoCreateObjectiveHud = true;
    [TextArea(2, 3)]
    public string objectiveMessage = "Use the washing machine to distract Cikgu, then both players reach the checkpoint.";
    public TMP_Text objectiveText;
    public TMP_Text checkpointStatusText;
    public TMP_Text coOpStatusText;
    public TMP_Text keyHintText;
    public TMP_Text washingMachineStatusText;
    public Slider washingMachineTimerSlider;
    public Image washingMachineTimerFill;
    public Slider suspicionSlider;
    public Image suspicionFill;
    public WashingMachine trackedWashingMachine;

    [Header("Sound Balance")]
    public bool applyLevel2SoundBalance = true;
    public float tunedSneakRange = 1.5f;
    public float tunedQuietRange = 4f;
    public float tunedWalkRange = 8f;
    public float tunedRunRange = 24f;
    public float tunedCikguWalkHearingRange = 7f;
    public float tunedCikguRunHearingRange = 13f;
    
    // State tracking
    private bool isGameOver = false;
    private bool isVictory = false;
    private bool isRestarting = false;
    private bool isGoingToMenu = false;
    private bool gameOverUiBuilt = false;
    private HashSet<int> playersAtCheckpoint = new HashSet<int>();
    private GameObject objectiveHudRoot;
    
    // ==================== INITIALIZATION ====================
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        SetupGameOverPanel();

        // Hide panels
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
        if (cutsceneObject != null)
            cutsceneObject.SetActive(false);
        
        SetupButtons();
        SetupObjectiveHud();
    }
    
    void Start()
    {
        // Find Cikgu if not assigned
        if (cikguNPC == null)
        {
            cikguNPC = FindObjectOfType<CikguNPC>();
        }

        if (trackedWashingMachine == null)
        {
            trackedWashingMachine = FindBestWashingMachine();
        }
        
        // Subscribe to Cikgu's catch event
        CikguNPC.OnPlayerCaught += OnPlayerCaughtByCikgu;
        
        // Reset flags on scene start
        isGameOver = false;
        isVictory = false;
        isRestarting = false;

        // Master-driven restart (PhotonNetwork.LoadLevel) only stays in sync if
        // every client has this on. It is set at connect, but re-assert it here
        // so a restart can never leave one player on a stale scene.
        PhotonNetwork.AutomaticallySyncScene = true;

        ApplySoundBalance();
        
        GameLog.Log("[Level2] Level 2 Manager initialized - Team Game Over Mode");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        CikguNPC.OnPlayerCaught -= OnPlayerCaughtByCikgu;
    }

    void SetupGameOverPanel()
    {
        if (gameOverPanel == null)
            CreateGameOverCanvas();
        else
            NormalizePanelRoot(gameOverPanel);

        BuildGameOverCard();
    }

    /// <summary>Kept for backward compatibility (called from ShowGameOverUI).</summary>
    void StyleGameOverPanel()
    {
        BuildGameOverCard();
    }

    void CreateGameOverCanvas()
    {
        GameObject canvasObject = new GameObject("Level2GameOverCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvasObject.transform, false);
        NormalizePanelRoot(gameOverPanel);
    }

    /// <summary>Force the panel root to be a clean, full-screen overlay (the
    /// scene shipped it with an odd scale / offset - reset it).</summary>
    void NormalizePanelRoot(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null) rect = panel.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Builds the redesigned "caught" screen: a full-screen scrim with a
    /// centered dark card, red alert accents, title, subtitle, tip and two
    /// buttons. Idempotent - safe to call from Awake and from ShowGameOverUI.
    /// Any legacy layout that shipped in the scene is cleared first so the new
    /// design always wins.
    /// </summary>
    void BuildGameOverCard()
    {
        if (gameOverUiBuilt || gameOverPanel == null) return;
        gameOverUiBuilt = true;

        Transform root = gameOverPanel.transform;

        // Wipe any legacy children the scene shipped with.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        // Full-screen dark scrim.
        Image scrim = gameOverPanel.GetComponent<Image>();
        if (scrim == null) scrim = gameOverPanel.AddComponent<Image>();
        scrim.color = new Color(0.016f, 0.02f, 0.028f, 0.93f);
        scrim.raycastTarget = true;

        // ---- Card ----
        GameObject card = new GameObject("GameOverCard");
        card.transform.SetParent(root, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(860f, 520f);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.066f, 0.078f, 0.094f, 0.99f);

        // Accents: top alert bar + left rail + faint bottom line.
        AddGameOverStrip(card.transform, "TopAlertBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), Vector2.zero, new Color(0.90f, 0.19f, 0.15f, 1f));
        AddGameOverStrip(card.transform, "LeftRail", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(7f, -10f), new Color(0.90f, 0.19f, 0.15f, 0.85f));
        AddGameOverStrip(card.transform, "BottomLine", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f), new Color(1f, 1f, 1f, 0.06f));

        // Eyebrow tag.
        TMP_Text eyebrow = CreateGameOverText("Eyebrow", card.transform, new Vector2(60f, -84f), new Vector2(-60f, -50f), 22f, TextAlignmentOptions.Center);
        eyebrow.text = "LEVEL 2  //  MISSION FAILED";
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.characterSpacing = 7f;
        eyebrow.enableAutoSizing = false;
        eyebrow.color = new Color(1f, 0.44f, 0.34f, 0.92f);

        // Title.
        gameOverText = CreateGameOverText("GameOverTitle", card.transform, new Vector2(40f, -212f), new Vector2(-40f, -96f), 88f, TextAlignmentOptions.Center);
        gameOverText.text = "CAUGHT!";
        gameOverText.fontStyle = FontStyles.Bold;
        gameOverText.characterSpacing = 2f;
        gameOverText.color = new Color(1f, 0.30f, 0.24f, 1f);

        // Divider under the title.
        AddGameOverStrip(card.transform, "Divider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150f, -232f), new Vector2(150f, -230f), new Color(1f, 1f, 1f, 0.14f));

        // Subtitle.
        gameOverSubtext = CreateGameOverText("GameOverSubtext", card.transform, new Vector2(80f, -322f), new Vector2(-80f, -252f), 27f, TextAlignmentOptions.Center);
        gameOverSubtext.text = "Both players lose this run.";
        gameOverSubtext.color = new Color(0.88f, 0.92f, 0.97f, 1f);

        // Tip line.
        TMP_Text hint = CreateGameOverText("Hint", card.transform, new Vector2(90f, -376f), new Vector2(-90f, -336f), 18f, TextAlignmentOptions.Center);
        hint.text = "Tip: start the washing machine to pull Cikgu away, then slip out together.";
        hint.fontStyle = FontStyles.Italic;
        hint.color = new Color(0.62f, 0.69f, 0.78f, 0.9f);

        // Buttons.
        retryButton = CreateGameOverButton("RetryButton", card.transform, "RETRY", new Vector2(-146f, 66f), new Color(0.90f, 0.22f, 0.17f, 1f));
        mainMenuButton = CreateGameOverButton("MainMenuButton", card.transform, "MAIN MENU", new Vector2(146f, 66f), new Color(0.12f, 0.15f, 0.18f, 1f));
    }

    void AddGameOverStrip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject stripObject = new GameObject(name);
        stripObject.transform.SetParent(parent, false);
        RectTransform rect = stripObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image strip = stripObject.AddComponent<Image>();
        strip.color = color;
        strip.raycastTarget = false;
    }

    TMP_Text CreateGameOverText(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    Button CreateGameOverButton(string name, Transform parent, string label, Vector2 anchoredPosition, Color color)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(250f, 62f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();

        TMP_Text buttonText = CreateGameOverText("Label", buttonObject.transform, new Vector2(12f, -46f), new Vector2(-12f, -14f), 21f, TextAlignmentOptions.Center);
        buttonText.text = label;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = Color.white;

        StyleGameOverButton(button, color, Color.white);
        return button;
    }

    void StyleGameOverButton(Button button, Color normalColor, Color textColor)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normalColor;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontStyle = FontStyles.Bold;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
    }
    
    void SetupButtons()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RestartLevel);
            
            TMP_Text buttonText = retryButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null) buttonText.text = "RETRY";
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMenu);
            
            TMP_Text buttonText = mainMenuButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null) buttonText.text = "MAIN MENU";
        }
    }
    
    void Update()
    {
        if (!isGameOver && !isVictory)
        {
            CheckWinCondition();
        }

        UpdateObjectiveHud();
    }

    void SetupObjectiveHud()
    {
        if (!autoCreateObjectiveHud) return;
        if (objectiveText != null && checkpointStatusText != null && suspicionSlider != null) return;

        objectiveHudRoot = new GameObject("Level2ObjectiveHUD");
        Canvas canvas = objectiveHudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = objectiveHudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        objectiveHudRoot.AddComponent<GraphicRaycaster>();

        RectTransform panel = CreateHudPanel(objectiveHudRoot.transform);

        if (objectiveText == null)
        {
            objectiveText = CreateHudBlock("ObjectiveBlock", panel, 52f, 72f, new Color(0.35f, 0.8f, 1f, 1f), "OBJECTIVE", 21f);
            objectiveText.text = objectiveMessage;
        }

        if (checkpointStatusText == null)
        {
            checkpointStatusText = CreateHudBlock("CheckpointBlock", panel, 132f, 46f, new Color(0.3f, 1f, 0.55f, 1f), "CHECKPOINT", 17f);
        }

        if (coOpStatusText == null)
        {
            coOpStatusText = CreateHudBlock("TeamBlock", panel, 186f, 52f, new Color(1f, 0.78f, 0.25f, 1f), "TEAM", 16f);
        }

        if (keyHintText == null)
        {
            keyHintText = CreateHudBlock("KeyBlock", panel, 246f, 50f, new Color(0.8f, 0.65f, 1f, 1f), "GRILL KEY", 16f);
        }

        if (washingMachineStatusText == null)
        {
            washingMachineStatusText = CreateHudBlock("LaundryBlock", panel, 304f, 52f, new Color(0.35f, 0.8f, 1f, 1f), "WASHING MACHINE", 17f);
        }

        if (washingMachineTimerSlider == null)
        {
            washingMachineTimerSlider = CreateMiniSlider("LaundryCountdown", panel, 366f, out washingMachineTimerFill);
        }

        if (suspicionSlider == null)
        {
            suspicionSlider = CreateSuspicionSlider(panel);
        }
    }

    RectTransform CreateHudPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("ObjectivePanel");
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(720f, 424f);

        Image bg = panelObject.AddComponent<Image>();
        bg.color = new Color(0.025f, 0.035f, 0.045f, 0.88f);

        GameObject topStrip = new GameObject("TopStrip");
        topStrip.transform.SetParent(panelObject.transform, false);
        RectTransform stripRect = topStrip.AddComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.offsetMin = new Vector2(0f, -6f);
        stripRect.offsetMax = Vector2.zero;
        Image strip = topStrip.AddComponent<Image>();
        strip.color = new Color(0.35f, 0.8f, 1f, 0.95f);

        GameObject railObject = new GameObject("LeftRail");
        railObject.transform.SetParent(panelObject.transform, false);
        RectTransform railRect = railObject.AddComponent<RectTransform>();
        railRect.anchorMin = new Vector2(0f, 0f);
        railRect.anchorMax = new Vector2(0f, 1f);
        railRect.offsetMin = new Vector2(0f, 0f);
        railRect.offsetMax = new Vector2(5f, -6f);
        Image rail = railObject.AddComponent<Image>();
        rail.color = new Color(0.18f, 0.55f, 0.7f, 0.9f);

        TMP_Text title = CreateHudText("HudTitle", rect, new Vector2(18f, -42f), new Vector2(-190f, -12f), 21f);
        title.text = "LEVEL 2 // STEALTH STATUS";
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.92f, 0.98f, 1f, 1f);
        title.enableWordWrapping = false;

        TMP_Text tag = CreateHudText("HudTag", rect, new Vector2(548f, -40f), new Vector2(-18f, -14f), 14f);
        tag.text = "CIKGU WATCH";
        tag.fontStyle = FontStyles.Bold;
        tag.alignment = TextAlignmentOptions.Right;
        tag.color = new Color(1f, 0.78f, 0.25f, 1f);
        tag.enableWordWrapping = false;

        return rect;
    }

    TMP_Text CreateHudBlock(string name, Transform parent, float top, float height, Color accentColor, string labelText, float bodySize)
    {
        GameObject blockObject = new GameObject(name);
        blockObject.transform.SetParent(parent, false);

        RectTransform rect = blockObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(18f, -(top + height));
        rect.offsetMax = new Vector2(-18f, -top);

        Image bg = blockObject.AddComponent<Image>();
        bg.color = new Color(0.075f, 0.095f, 0.11f, 0.86f);

        GameObject accentObject = new GameObject("Accent");
        accentObject.transform.SetParent(blockObject.transform, false);
        RectTransform accentRect = accentObject.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = new Vector2(5f, 0f);
        Image accent = accentObject.AddComponent<Image>();
        accent.color = accentColor;

        TMP_Text label = CreateHudText("Label", rect, new Vector2(16f, -24f), new Vector2(-16f, -6f), 12f);
        label.text = labelText;
        label.fontStyle = FontStyles.Bold;
        label.color = accentColor;
        label.enableWordWrapping = false;

        TMP_Text body = CreateHudText("Body", rect, new Vector2(16f, -height + 7f), new Vector2(-16f, -27f), bodySize);
        body.fontStyle = FontStyles.Bold;
        body.color = new Color(0.94f, 0.97f, 1f, 1f);
        body.enableAutoSizing = true;
        body.fontSizeMin = 12f;
        body.fontSizeMax = bodySize;

        return body;
    }

    TMP_Text CreateHudText(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float fontSize)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Left;
        text.overflowMode = TextOverflowModes.Ellipsis;

        return text;
    }

    Slider CreateSuspicionSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject("SuspicionMeter");
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.offsetMin = new Vector2(18f, -406f);
        sliderRect.offsetMax = new Vector2(-18f, -388f);

        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.09f, 0.95f);

        TMP_Text label = CreateHudText("SuspicionLabel", parent, new Vector2(18f, -385f), new Vector2(-18f, -370f), 12f);
        label.text = "CIKGU ALERT LEVEL";
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.92f, 0.98f, 1f, 0.78f);
        label.enableWordWrapping = false;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(sliderObject.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        suspicionFill = fillObject.AddComponent<Image>();
        suspicionFill.color = new Color(0.25f, 0.9f, 0.35f, 1f);

        for (int i = 1; i < 5; i++)
        {
            GameObject tickObject = new GameObject($"Tick{i}");
            tickObject.transform.SetParent(sliderObject.transform, false);
            RectTransform tickRect = tickObject.AddComponent<RectTransform>();
            tickRect.anchorMin = new Vector2(i / 5f, 0f);
            tickRect.anchorMax = new Vector2(i / 5f, 1f);
            tickRect.offsetMin = new Vector2(-1f, 0f);
            tickRect.offsetMax = new Vector2(1f, 0f);
            Image tick = tickObject.AddComponent<Image>();
            tick.color = new Color(1f, 1f, 1f, 0.22f);
        }

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.fillRect = fillRect;
        slider.targetGraphic = suspicionFill;

        return slider;
    }

    Slider CreateMiniSlider(string name, Transform parent, float top, out Image fillImage)
    {
        GameObject sliderObject = new GameObject(name);
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.offsetMin = new Vector2(18f, -(top + 14f));
        sliderRect.offsetMax = new Vector2(-18f, -top);

        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.065f, 0.075f, 0.95f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(sliderObject.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        fillImage = fillObject.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.8f, 1f, 1f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;

        return slider;
    }

    void UpdateObjectiveHud()
    {
        if (objectiveText != null)
        {
            objectiveText.text = GetObjectiveMessage();
        }

        if (checkpointStatusText != null)
        {
            int required = LevelCheckpoint.LastRequiredPlayers > 0 ? LevelCheckpoint.LastRequiredPlayers : requiredPlayersAtCheckpoint;
            int current = LevelCheckpoint.LastRequiredPlayers > 0 ? LevelCheckpoint.LastPlayersInCheckpoint : playersAtCheckpoint.Count;
            checkpointStatusText.text = $"{current}/{required} players at the exit";
        }

        if (coOpStatusText != null)
        {
            coOpStatusText.text = GetCoOpStatus();
        }

        if (keyHintText != null)
        {
            keyHintText.text = GetGrillKeyHint();
        }

        UpdateWashingMachineHud();

        if (suspicionSlider != null && cikguNPC != null)
        {
            float normalized = Mathf.Clamp01(cikguNPC.Suspicion / 100f);
            suspicionSlider.value = normalized;

            if (suspicionFill != null)
            {
                if (normalized >= 0.85f)
                    suspicionFill.color = new Color(1f, 0.15f, 0.12f, 1f);
                else if (normalized >= 0.45f)
                    suspicionFill.color = new Color(1f, 0.78f, 0.2f, 1f);
                else
                    suspicionFill.color = new Color(0.25f, 0.9f, 0.35f, 1f);
            }
        }
    }

    void UpdateWashingMachineHud()
    {
        if (washingMachineStatusText == null && washingMachineTimerSlider == null) return;

        WashingMachine machine = FindBestWashingMachine();
        if (machine != null)
            trackedWashingMachine = machine;

        if (trackedWashingMachine == null)
        {
            if (washingMachineStatusText != null)
                washingMachineStatusText.text = "Find the laundry room and set a distraction timer.";

            if (washingMachineTimerSlider != null)
                washingMachineTimerSlider.value = 0f;

            return;
        }

        if (trackedWashingMachine.IsTimerActive)
        {
            int seconds = Mathf.CeilToInt(trackedWashingMachine.CurrentTimer);
            if (washingMachineStatusText != null)
                washingMachineStatusText.text = $"Starting in {FormatSeconds(seconds)}. Move before Cikgu checks it.";
            SetWashingMachineMeter(
                Mathf.Clamp01(trackedWashingMachine.CurrentTimer / trackedWashingMachine.CurrentTimerDuration),
                new Color(0.35f, 0.8f, 1f, 1f));
            return;
        }

        if (trackedWashingMachine.isRunning)
        {
            int seconds = Mathf.CeilToInt(trackedWashingMachine.RunTimeRemaining);
            if (washingMachineStatusText != null)
                washingMachineStatusText.text = $"Running distraction: {FormatSeconds(seconds)} noise left.";
            SetWashingMachineMeter(
                Mathf.Clamp01(trackedWashingMachine.RunTimeRemaining / Mathf.Max(0.01f, trackedWashingMachine.runDuration)),
                new Color(1f, 0.78f, 0.25f, 1f));
            return;
        }

        if (washingMachineStatusText != null)
            washingMachineStatusText.text = "Ready. Set the timer, then leave the room quietly.";
        SetWashingMachineMeter(0f, new Color(0.35f, 0.8f, 1f, 1f));
    }

    void SetWashingMachineMeter(float value, Color color)
    {
        if (washingMachineTimerSlider != null)
            washingMachineTimerSlider.value = value;

        if (washingMachineTimerFill != null)
            washingMachineTimerFill.color = color;
    }

    string FormatSeconds(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    WashingMachine FindBestWashingMachine()
    {
        WashingMachine[] machines = FindObjectsByType<WashingMachine>(FindObjectsSortMode.None);
        if (machines == null || machines.Length == 0) return trackedWashingMachine;

        GameObject localPlayer = GetLocalPlayerObject();
        Vector3 origin = localPlayer != null ? localPlayer.transform.position : transform.position;

        WashingMachine best = null;
        float bestScore = float.MaxValue;

        foreach (WashingMachine machine in machines)
        {
            if (machine == null || !machine.gameObject.activeInHierarchy) continue;

            float score = Vector3.Distance(origin, machine.transform.position);
            if (machine.IsTimerActive) score -= 1000f;
            else if (machine.isRunning) score -= 700f;

            if (score < bestScore)
            {
                best = machine;
                bestScore = score;
            }
        }

        return best != null ? best : trackedWashingMachine;
    }

    string GetObjectiveMessage()
    {
        if (isGameOver) return "Caught by Cikgu. Retry with both players.";
        if (isVictory) return "Level complete. Moving to the next scene...";
        if (cikguNPC == null) return objectiveMessage;

        float suspicion = cikguNPC.Suspicion;
        if (suspicion >= 100f || cikguNPC.CurrentState == CikguState.Chasing)
            return "Cikgu spotted you. Break line of sight and run.";

        if (suspicion >= cikguNPC.investigateThreshold)
            return "Cikgu is suspicious. Hide, slow down, or use the distraction.";

        if (cikguNPC.CurrentState == CikguState.WalkingToSound ||
            cikguNPC.CurrentState == CikguState.LookingAround ||
            cikguNPC.CurrentState == CikguState.Patrolling)
            return "Cikgu is distracted. Move together toward the checkpoint.";

        return objectiveMessage;
    }

    void ApplySoundBalance()
    {
        if (!applyLevel2SoundBalance) return;

        SoundDetectionSystem soundSystem = SoundDetectionSystem.Instance;
        if (soundSystem != null)
        {
            soundSystem.veryQuietRange = tunedSneakRange;
            soundSystem.quietRange = tunedQuietRange;
            soundSystem.mediumRange = tunedWalkRange;
            soundSystem.loudRange = tunedRunRange;
        }

        if (cikguNPC != null)
        {
            cikguNPC.mediumFootstepHearingRange = tunedCikguWalkHearingRange;
            cikguNPC.loudFootstepHearingRange = tunedCikguRunHearingRange;
            cikguNPC.footstepHearingRange = tunedCikguRunHearingRange;
        }
    }

    string GetCoOpStatus()
    {
        int required = LevelCheckpoint.LastRequiredPlayers > 0 ? LevelCheckpoint.LastRequiredPlayers : requiredPlayersAtCheckpoint;
        int current = LevelCheckpoint.LastRequiredPlayers > 0 ? LevelCheckpoint.LastPlayersInCheckpoint : playersAtCheckpoint.Count;

        if (current > 0 && current < required)
            return "One player is waiting at the checkpoint.";

        if (current >= required)
            return "Both players are ready.";

        GameObject localPlayer = GetLocalPlayerObject();
        GameObject partner = GetPartnerPlayerObject(localPlayer);
        if (localPlayer == null || partner == null)
            return "Stay together and avoid running near Cikgu.";

        float distance = Vector3.Distance(localPlayer.transform.position, partner.transform.position);
        if (distance > 18f)
            return $"Partner is far away ({Mathf.RoundToInt(distance)}m). Regroup before escaping.";

        if (distance > 8f)
            return $"Partner nearby ({Mathf.RoundToInt(distance)}m). Move together.";

        return "Partner is close. Good time to coordinate.";
    }

    string GetGrillKeyHint()
    {
        if (InventoryHasGrillKey(true))
            return "You have it. Find the locked grill door.";

        if (InventoryHasGrillKey(false))
            return "Your partner has it. Meet at the grill door.";

        GameObject localPlayer = GetLocalPlayerObject();
        GrillKey nearestKey = FindNearestActiveGrillKey(localPlayer != null ? localPlayer.transform.position : transform.position, out float distance);

        if (nearestKey == null)
            return "Not visible. Check nearby rooms or your partner's inventory.";

        if (localPlayer == null)
            return $"{Mathf.RoundToInt(distance)}m away.";

        string direction = DirectionFrom(localPlayer.transform, nearestKey.transform.position);
        return $"{Mathf.RoundToInt(distance)}m {direction}.";
    }

    bool InventoryHasGrillKey(bool localOnly)
    {
        InventorySystem[] inventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        foreach (InventorySystem inventory in inventories)
        {
            if (inventory == null) continue;

            PhotonView pv = inventory.GetComponent<PhotonView>();
            bool isLocal = !PhotonNetwork.InRoom || pv == null || pv.IsMine;
            if (localOnly != isLocal) continue;

            foreach (ItemData item in inventory.items)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemName) && item.itemName.Contains("Grill Key"))
                    return true;
            }
        }

        return false;
    }

    GrillKey FindNearestActiveGrillKey(Vector3 origin, out float nearestDistance)
    {
        nearestDistance = float.MaxValue;
        GrillKey nearest = null;
        GrillKey[] keys = FindObjectsByType<GrillKey>(FindObjectsSortMode.None);

        foreach (GrillKey key in keys)
        {
            if (key == null || !key.gameObject.activeInHierarchy) continue;

            float distance = Vector3.Distance(origin, key.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = key;
            }
        }

        return nearest;
    }

    string DirectionFrom(Transform from, Vector3 target)
    {
        Vector3 toTarget = target - from.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return "here";

        toTarget.Normalize();
        float forward = Vector3.Dot(from.forward, toTarget);
        float right = Vector3.Dot(from.right, toTarget);

        if (forward > 0.65f) return "ahead";
        if (forward < -0.65f) return "behind";
        return right >= 0f ? "to your right" : "to your left";
    }

    GameObject GetLocalPlayerObject()
    {
        GameObject taggedObject = PhotonNetwork.LocalPlayer?.TagObject as GameObject;
        if (taggedObject != null) return taggedObject;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            if (!PhotonNetwork.InRoom || pv == null || pv.IsMine)
                return player;
        }

        return null;
    }

    GameObject GetPartnerPlayerObject(GameObject localPlayer)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player == null || player == localPlayer) continue;

            PhotonView pv = player.GetComponent<PhotonView>();
            if (!PhotonNetwork.InRoom || pv == null || !pv.IsMine)
                return player;
        }

        return null;
    }
    
    // ==================== GAME OVER (Cikgu Catches Player) ====================
    
    /// <summary>
    /// Called when Cikgu catches ANY player
    /// </summary>
    void OnPlayerCaughtByCikgu(GameObject caughtPlayer)
    {
        if (isGameOver || isVictory || isRestarting) return;
        
        GameLog.Log($"[Level2] Player caught by Cikgu: {caughtPlayer?.name}");
        
        // Team game over - notify ALL players
        // Only Master sends the RPC to prevent double calls
        if (teamGameOver && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_TeamGameOver", RpcTarget.All, caughtPlayer?.name ?? "Player");
        }
    }
    
    [PunRPC]
    void RPC_TeamGameOver(string caughtPlayerName)
    {
        if (isGameOver || isRestarting) return;
        isGameOver = true;
        
        GameLog.Log($"[Level2] TEAM GAME OVER! {caughtPlayerName} was caught!");
        
        // Freeze ALL players
        FreezeAllPlayers();
        
        // Show game over UI
        ShowGameOverUI(caughtPlayerName);
        
        // Play sound
        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);
        
        // Show cursor
        CursorManager.SetFree();
        
        // NOTE: Don't pause Time.timeScale - causes issues with RPC
    }
    
    void FreezeAllPlayers()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            FreezePlayer(player);
        }
    }
    
    void FreezePlayer(GameObject player)
    {
        if (player == null) return;
        
        GameLog.Log($"[Level2] Freezing player: {player.name}");
        
        // Disable CharacterController
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        
        // Disable movement scripts
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            string name = script.GetType().Name;
            
            // Skip Photon scripts
            if (name == "PhotonView" || 
                name == "PhotonTransformView" || 
                name == "PhotonAnimatorView")
                continue;
            
            if (name.Contains("Controller") || 
                name.Contains("Movement") || 
                name.Contains("Input") ||
                name.Contains("Player"))
            {
                script.enabled = false;
            }
        }
        
        // Stop animator
        var animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetFloat("Speed", 0);
            animator.SetBool("Grounded", true);
        }
        
        // Freeze rigidbody
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
    
    void ShowGameOverUI(string caughtPlayerName)
    {
        if (gameOverPanel == null)
            SetupGameOverPanel();

        if (objectiveHudRoot != null)
            objectiveHudRoot.SetActive(false);

        if (gameOverPanel != null)
        {
            StyleGameOverPanel();
            gameOverPanel.SetActive(true);
            
            if (gameOverText != null)
            {
                gameOverText.text = "CAUGHT!";
            }

            if (gameOverSubtext != null)
            {
                gameOverSubtext.text = $"Cikgu caught {caughtPlayerName}.\nBoth players lose this run.";
            }
        }
    }
    
    // ==================== WIN CONDITION ====================
    
    public void PlayerEnteredCheckpoint(int actorNumber)
    {
        if (isGameOver || isVictory) return;
        
        if (!playersAtCheckpoint.Contains(actorNumber))
        {
            playersAtCheckpoint.Add(actorNumber);
            photonView.RPC("RPC_PlayerAtCheckpoint", RpcTarget.AllBuffered, actorNumber, true);
        }
    }
    
    public void PlayerExitedCheckpoint(int actorNumber)
    {
        if (isGameOver || isVictory) return;
        
        if (playersAtCheckpoint.Contains(actorNumber))
        {
            playersAtCheckpoint.Remove(actorNumber);
            photonView.RPC("RPC_PlayerAtCheckpoint", RpcTarget.AllBuffered, actorNumber, false);
        }
    }
    
    [PunRPC]
    void RPC_PlayerAtCheckpoint(int actorNumber, bool entered)
    {
        if (entered)
        {
            playersAtCheckpoint.Add(actorNumber);
            GameLog.Log($"[Level2] Player {actorNumber} at checkpoint ({playersAtCheckpoint.Count}/{requiredPlayersAtCheckpoint})");
        }
        else
        {
            playersAtCheckpoint.Remove(actorNumber);
            GameLog.Log($"[Level2] Player {actorNumber} left checkpoint ({playersAtCheckpoint.Count}/{requiredPlayersAtCheckpoint})");
        }
    }
    
    void CheckWinCondition()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        
        if (playersAtCheckpoint.Count >= requiredPlayersAtCheckpoint)
        {
            TriggerVictory();
        }
    }
    
    void TriggerVictory()
    {
        if (isVictory || isGameOver) return;
        isVictory = true;
        
        GameLog.Log("[Level2] VICTORY! All players reached checkpoint!");
        
        if (audioSource != null && victorySound != null)
            audioSource.PlayOneShot(victorySound);
        
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            
            if (victoryText != null)
            {
                if (isFinalLevel)
                {
                    victoryText.text = "TAHNIAH!\nAnda Berjaya Melarikan Diri!";
                }
                else
                {
                    victoryText.text = "LEVEL SELESAI!\nMenuju ke level seterusnya...";
                }
            }
        }
        
        if (cutsceneObject != null)
            cutsceneObject.SetActive(true);
        
        if (PhotonNetwork.IsMasterClient)
        {
            if (isFinalLevel)
            {
                Invoke(nameof(ReturnToMenuAfterWin), transitionDelay);
            }
            else
            {
                Invoke(nameof(LoadNextLevel), transitionDelay);
            }
        }
    }
    
    void LoadNextLevel()
    {
        GameLog.Log($"[Level2] Loading next level: {nextLevelScene}");
        PhotonNetwork.LoadLevel(nextLevelScene);
    }
    
    void ReturnToMenuAfterWin()
    {
        PhotonNetwork.LeaveRoom();
    }
    
    // ==================== RESTART LEVEL ====================
    // Following Level1Manager pattern
    
    /// <summary>
    /// ANY player can call this to restart for EVERYONE
    /// </summary>
    public void RestartLevel()
    {
        if (isRestarting) return;
        isRestarting = true;
        
        GameLog.Log("[Level2] RestartLevel called!");
        
        Time.timeScale = 1f;
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // ✅ KEY FIX: Send RPC to ALL players to restart
            // This ensures everyone restarts, not just master
            photonView.RPC("RPC_RestartForEveryone", RpcTarget.All);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    
    /// <summary>
    /// Received by ALL players. Everyone cleans up their own networked player,
    /// then ONLY the master triggers the actual scene load. Because
    /// PhotonNetwork.AutomaticallySyncScene is enabled, every client follows the
    /// master's PhotonNetwork.LoadLevel in lockstep.
    /// </summary>
    [PunRPC]
    void RPC_RestartForEveryone()
    {
        GameLog.Log("[Level2] RPC_RestartForEveryone received!");
        isRestarting = true;

        // Reset state
        Time.timeScale = 1f;
        CursorManager.SetLocked();

        // Hide UI
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        // Each client removes its OWN player from the room cache first.
        CleanupBeforeRestart();

        // EVERY client reloads itself here. PhotonNetwork.LoadLevel always loads
        // the scene locally AND pauses this client's network queue during the
        // load - that both restarts both players and prevents the teammate-spawn
        // race (no partner Instantiate is applied to a tearing-down scene).
        //
        // A master-only LoadLevel does NOT work for a restart: reloading the SAME
        // scene doesn't change the synced scene room-property, so the other client
        // is never told to follow and simply never reloads.
        PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
    }

    void CleanupBeforeRestart()
    {
        GameLog.Log("[Level2] Cleaning up before restart...");

        // NOTE: do NOT PhotonNetwork.Destroy the player here. It races with the
        // master's PhotonNetwork.LoadLevel and throws "Destroy Failed. Could not
        // find PhotonView..." on whichever client has already torn the object
        // down. The scene reload destroys the player objects for us, and
        // PlayerSpawnerPun re-instantiates a fresh one on load.
        PhotonNetwork.LocalPlayer.TagObject = null;

        // Reset local state
        isGameOver = false;
        isVictory = false;
        playersAtCheckpoint.Clear();
    }

    // ==================== RETURN TO MENU ====================
    
    public void ReturnToMenu()
    {
        if (isGoingToMenu) return;
        isGoingToMenu = true;
        
        GameLog.Log("[Level2] Returning to menu...");
        
        Time.timeScale = 1f;
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }
    
    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
    
    // ==================== DEBUG ====================
    
    void OnDrawGizmosSelected()
    {
        if (winCheckpoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(winCheckpoint.position, checkpointRadius);
        }
    }
}
