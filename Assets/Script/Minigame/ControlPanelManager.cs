using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;

/// <summary>
/// ControlPanelManager.cs - Master controller for the CCTV puzzle
/// UPDATED: Shows Game Over screen when puzzle fails
/// </summary>
public class ControlPanelManager : MonoBehaviourPun
{
    public static ControlPanelManager Instance { get; private set; }
    
    [Header("Game Settings")]
    public float totalTime = 90f;
    public int maxStrikes = 3;
    
    [Header("Module References")]
    public WiresModule wiresModule;
    public KeypadModule keypadModule;
    public MemoryModule memoryModule;
    
    [Header("UI References")]
    public GameObject panelUI;
    public TMP_Text timerText;
    public Image[] strikeIndicators;
    public TMP_Text moduleStatusText;
    public GameObject successScreen;
    public GameObject failScreen;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip strikeSound;
    public AudioClip successSound;
    public AudioClip failSound;
    public AudioClip moduleCompleteSound;
    public AudioClip tickSound;
    
    [Header("Game Effects")]
    public Light[] cctvLights;
    public GameObject[] cctvCameras;
    
    // Game State
    private float currentTime;
    private int currentStrikes = 0;
    private bool isPuzzleActive = false;
    private bool isPuzzleComplete = false;
    private int currentModule = 0;
    
    // Operator tracking
    private int operatorActorNumber = -1;
    private bool isLocalPlayerOperator = false;
    
    // Module completion flags
    private bool wiresComplete = false;
    private bool keypadComplete = false;
    private bool memoryComplete = false;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        if (panelUI != null)
            panelUI.SetActive(false);
        if (successScreen != null)
            successScreen.SetActive(false);
        if (failScreen != null)
            failScreen.SetActive(false);
        
        UpdateStrikeUI();
        DisableAllModules();
    }
    
    void Update()
    {
        if (!isPuzzleActive || isPuzzleComplete) return;
        
        if (isLocalPlayerOperator)
        {
            UpdateTimer();
        }
    }
    
    void UpdateTimer()
    {
        currentTime -= Time.deltaTime;
        
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60);
            int seconds = Mathf.FloorToInt(currentTime % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
            
            if (currentTime <= 10f)
            {
                timerText.color = Color.red;
                
                if (Mathf.FloorToInt(currentTime) != Mathf.FloorToInt(currentTime + Time.deltaTime))
                {
                    PlaySound(tickSound);
                }
            }
        }
        
        if (currentTime <= 0)
        {
            currentTime = 0;
            photonView.RPC("RPC_PuzzleFailed", RpcTarget.All, "Masa tamat!");
        }
    }
    
    // ==================== PUZZLE START/END ====================
    
    public void StartPuzzle(int playerActorNumber)
    {
        if (operatorActorNumber != -1)
        {
            Debug.Log("Panel is already in use!");
            return;
        }
        
        photonView.RPC("RPC_StartPuzzle", RpcTarget.All, playerActorNumber);
    }
    
    [PunRPC]
    void RPC_StartPuzzle(int operatorActor)
    {
        operatorActorNumber = operatorActor;
        isPuzzleActive = true;
        isPuzzleComplete = false;
        currentTime = totalTime;
        currentStrikes = 0;
        currentModule = 0;
        
        wiresComplete = false;
        keypadComplete = false;
        memoryComplete = false;
        
        isLocalPlayerOperator = (PhotonNetwork.LocalPlayer.ActorNumber == operatorActor);
        
        if (isLocalPlayerOperator)
        {
            Debug.Log("You are the OPERATOR. Solve the puzzle!");
            
            if (panelUI != null)
                panelUI.SetActive(true);
            
            LockPlayerMovement(true);
            ActivateModule(0);
        }
        else
        {
            Debug.Log("You are the COORDINATOR. Help with the manual!");
            
            ManualUI manualUI = FindObjectOfType<ManualUI>();
            if (manualUI != null && manualUI.HasManual())
            {
                manualUI.SetCanToggle(true);
            }
        }
        
        UpdateStrikeUI();
        UpdateModuleStatusUI();
    }
    
    void LockPlayerMovement(bool locked)
    {
        GameObject localPlayer = PhotonNetwork.LocalPlayer.TagObject as GameObject;
        if (localPlayer != null)
        {
            var controller = localPlayer.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = !locked;
            
            var tpc = localPlayer.GetComponent<StarterAssets.ThirdPersonController>();
            if (tpc != null)
                tpc.enabled = !locked;
            
            var customTpc = localPlayer.GetComponent<CustomCharacterController.ThirdPersonController>();
            if (customTpc != null)
                customTpc.enabled = !locked;
        }
        
        Cursor.visible = locked;
        Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    // ==================== MODULE MANAGEMENT ====================
    
    void DisableAllModules()
    {
        if (wiresModule != null) wiresModule.SetActive(false);
        if (keypadModule != null) keypadModule.SetActive(false);
        if (memoryModule != null) memoryModule.SetActive(false);
    }
    
    void ActivateModule(int moduleIndex)
    {
        DisableAllModules();
        currentModule = moduleIndex;
        
        switch (moduleIndex)
        {
            case 0:
                if (wiresModule != null)
                {
                    wiresModule.SetActive(true);
                    wiresModule.Initialize();
                }
                break;
            case 1:
                if (keypadModule != null)
                {
                    keypadModule.SetActive(true);
                    keypadModule.Initialize();
                }
                break;
            case 2:
                if (memoryModule != null)
                {
                    memoryModule.SetActive(true);
                    memoryModule.Initialize();
                }
                break;
        }
        
        UpdateModuleStatusUI();
    }
    
    public void ModuleComplete(int moduleIndex)
    {
        if (!isLocalPlayerOperator) return;
        
        PlaySound(moduleCompleteSound);
        photonView.RPC("RPC_ModuleComplete", RpcTarget.All, moduleIndex);
    }
    
    [PunRPC]
    void RPC_ModuleComplete(int moduleIndex)
    {
        switch (moduleIndex)
        {
            case 0: wiresComplete = true; break;
            case 1: keypadComplete = true; break;
            case 2: memoryComplete = true; break;
        }
        
        Debug.Log($"Module {moduleIndex} complete!");
        
        if (wiresComplete && keypadComplete && memoryComplete)
        {
            PuzzleSuccess();
        }
        else if (isLocalPlayerOperator)
        {
            ActivateModule(moduleIndex + 1);
        }
    }
    
    // ==================== STRIKES ====================
    
    public void AddStrike(string reason = "")
    {
        if (!isLocalPlayerOperator) return;
        
        photonView.RPC("RPC_AddStrike", RpcTarget.All, reason);
    }
    
    [PunRPC]
    void RPC_AddStrike(string reason)
    {
        currentStrikes++;
        Debug.Log($"STRIKE {currentStrikes}! {reason}");
        
        if (isLocalPlayerOperator)
        {
            PlaySound(strikeSound);
            StartCoroutine(ScreenShake());
        }
        
        UpdateStrikeUI();
        
        if (currentStrikes >= maxStrikes)
        {
            photonView.RPC("RPC_PuzzleFailed", RpcTarget.All, "Terlalu banyak kesilapan!");
        }
    }
    
    IEnumerator ScreenShake()
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) yield break;
        
        Vector3 originalPos = cam.localPosition;
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-0.1f, 0.1f);
            float y = Random.Range(-0.1f, 0.1f);
            cam.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        cam.localPosition = originalPos;
    }
    
    // ==================== WIN/LOSE ====================
    
    void PuzzleSuccess()
    {
        isPuzzleComplete = true;
        isPuzzleActive = false;
        
        Debug.Log("PUZZLE COMPLETE! CCTV disabled!");
        
        if (isLocalPlayerOperator)
        {
            PlaySound(successSound);
            
            if (successScreen != null)
                successScreen.SetActive(true);
            
            DisableCCTV();
            
            // Trigger success cutscene (only Master Client)
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(TriggerSuccessCutsceneAfterDelay(2f));
            }
            else
            {
                StartCoroutine(ClosePanelAfterDelay(3f));
            }
        }
        
        ManualUI manualUI = FindObjectOfType<ManualUI>();
        if (manualUI != null)
            manualUI.SetCanToggle(false);
    }
    
    IEnumerator TriggerSuccessCutsceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Close panel UI
        if (panelUI != null)
            panelUI.SetActive(false);
        if (successScreen != null)
            successScreen.SetActive(false);
        
        // Trigger success cutscene
        MidLevelCutsceneManager cutsceneManager = FindObjectOfType<MidLevelCutsceneManager>();
        if (cutsceneManager != null)
        {
            Debug.Log("<color=green>[Panel] Triggering success cutscene!</color>");
            cutsceneManager.PlaySuccessCutscene();
        }
        else
        {
            Debug.LogError("MidLevelCutsceneManager not found in scene!");
            LockPlayerMovement(false);
            operatorActorNumber = -1;
            isLocalPlayerOperator = false;
        }
    }
    
    [PunRPC]
    void RPC_PuzzleFailed(string reason)
    {
        isPuzzleComplete = true;
        isPuzzleActive = false;
        
        Debug.Log($"PUZZLE FAILED! {reason}");
        
        if (isLocalPlayerOperator)
        {
            PlaySound(failSound);
            
            if (failScreen != null)
                failScreen.SetActive(true);
            
            // ✅ NEW: Show Game Over screen after fail screen
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(ShowGameOverAfterFailScreen());
            }
            else
            {
                StartCoroutine(ClosePanelAfterDelay(3f));
            }
        }
        
        // Make loud sound to attract NPCs
        if (SoundDetectionSystem.Instance != null)
        {
            SoundDetectionSystem.Instance.EmitSound(transform.position, SoundType.Loud, gameObject);
        }
        
        ManualUI manualUI = FindObjectOfType<ManualUI>();
        if (manualUI != null)
            manualUI.SetCanToggle(false);
    }
    
    // ✅ NEW METHOD: Show Game Over after fail screen
    IEnumerator ShowGameOverAfterFailScreen()
    {
        // Show fail screen for 3 seconds
        yield return new WaitForSeconds(3f);
        
        // Close panel UI
        if (panelUI != null)
            panelUI.SetActive(false);
        if (failScreen != null)
            failScreen.SetActive(false);
        
        // Keep players frozen (they failed!)
        // LockPlayerMovement stays true
        
        // Show Game Over screen
        MidLevelCutsceneManager cutsceneManager = FindObjectOfType<MidLevelCutsceneManager>();
        if (cutsceneManager != null)
        {
            Debug.Log("<color=red>[Panel] Puzzle failed - Showing Game Over screen!</color>");
            
            // Directly call OnCaughtCutsceneEnd to show Game Over (reuse the same logic)
            // We'll access it through a public method
            ShowGameOverScreen();
        }
        else
        {
            Debug.LogError("MidLevelCutsceneManager not found!");
            LockPlayerMovement(false);
            operatorActorNumber = -1;
            isLocalPlayerOperator = false;
        }
    }
    
    // ✅ NEW METHOD: Public method to show Game Over screen
    void ShowGameOverScreen()
    {
        // Find Game Over screen directly
        GameObject gameOverScreen = GameObject.Find("GameOverScreen");
        
        if (gameOverScreen != null)
        {
            Debug.Log("<color=yellow>[Panel] Activating Game Over screen directly!</color>");
            gameOverScreen.SetActive(true);
            
            // Show cursor for restart button
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Debug.LogError("[Panel] GameOverScreen not found by name!");
        }
    }
    
    void DisableCCTV()
    {
        foreach (Light light in cctvLights)
        {
            if (light != null)
                light.enabled = false;
        }
        
        foreach (GameObject cam in cctvCameras)
        {
            if (cam != null)
                cam.SetActive(false);
        }
    }
    
    IEnumerator ClosePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (panelUI != null)
            panelUI.SetActive(false);
        if (successScreen != null)
            successScreen.SetActive(false);
        if (failScreen != null)
            failScreen.SetActive(false);
        
        LockPlayerMovement(false);
        
        operatorActorNumber = -1;
        isLocalPlayerOperator = false;
    }
    
    // ==================== UI UPDATES ====================
    
    void UpdateStrikeUI()
    {
        for (int i = 0; i < strikeIndicators.Length; i++)
        {
            if (strikeIndicators[i] != null)
            {
                strikeIndicators[i].color = (i < currentStrikes) ? Color.red : Color.gray;
            }
        }
    }
    
    void UpdateModuleStatusUI()
    {
        if (moduleStatusText == null) return;
        
        string[] moduleNames = { "WAYAR", "SIMBOL", "MEMORI" };
        
        if (currentModule < moduleNames.Length)
        {
            moduleStatusText.text = $"MODUL: {moduleNames[currentModule]}";
        }
    }
    
    // ==================== HELPERS ====================
    
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    public float GetCurrentTime()
    {
        return currentTime;
    }
    
    public bool IsPuzzleActive()
    {
        return isPuzzleActive;
    }
    
    public bool IsLocalPlayerOperator()
    {
        return isLocalPlayerOperator;
    }
    
    public void CancelPuzzle()
    {
        if (!isLocalPlayerOperator) return;
        
        photonView.RPC("RPC_PuzzleCancelled", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_PuzzleCancelled()
    {
        isPuzzleActive = false;
        
        if (isLocalPlayerOperator)
        {
            if (panelUI != null)
                panelUI.SetActive(false);
            
            LockPlayerMovement(false);
        }
        
        operatorActorNumber = -1;
        isLocalPlayerOperator = false;
        
        ManualUI manualUI = FindObjectOfType<ManualUI>();
        if (manualUI != null)
            manualUI.SetCanToggle(false);
        
        Debug.Log("Puzzle cancelled.");
    }
}