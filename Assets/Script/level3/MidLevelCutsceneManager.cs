using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

/// <summary>
/// MidLevelCutsceneManager.cs - Handles mid-level cutscenes in Level 3
/// ENHANCED VERSION with better debugging
/// </summary>
public class MidLevelCutsceneManager : MonoBehaviourPun
{
    public static MidLevelCutsceneManager Instance { get; private set; }

    [Header("Cutscene Videos")]
    [Tooltip("Success cutscene after completing panel puzzle")]
    public VideoClip successCutscene;
    
    [Tooltip("Caught cutscene when guard catches players")]
    public VideoClip caughtCutscene;

    [Header("UI References")]
    public GameObject cutsceneCanvas;
    public RawImage videoDisplay;
    public VideoPlayer videoPlayer;

    [Header("Game Over Screen")]
    public GameObject gameOverScreen;
    public UnityEngine.UI.Button restartButton;

    [Header("Audio")]
    public AudioSource cutsceneAudio;

    // State
    private bool isCutscenePlaying = false;
    private CutsceneType currentCutsceneType;

    public enum CutsceneType
    {
        Success,
        Caught
    }

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
    }

    void Start()
    {
        SetupComponents();
        HideAllScreens();

        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }
        
        // ✅ DEBUG: Check Game Over screen setup
        if (gameOverScreen != null)
        {
            GameLog.Log($"<color=cyan>[CutsceneManager] Game Over screen assigned: {gameOverScreen.name}</color>");
            GameLog.Log($"   - Initial active state: {gameOverScreen.activeSelf}");
            
            Canvas canvas = gameOverScreen.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameLog.Log($"   - Canvas found: {canvas.name}, Sort Order: {canvas.sortingOrder}");
            }
            else
            {
                Debug.LogError("   - ❌ NO CANVAS PARENT FOUND for Game Over screen!");
            }
        }
        else
        {
            Debug.LogError("<color=red>[CutsceneManager] ❌ Game Over screen NOT assigned!</color>");
        }
    }

    void SetupComponents()
    {
        // Auto-setup VideoPlayer if not assigned
        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        // Setup RenderTexture for video display
        if (videoDisplay != null)
        {
            if (videoDisplay.texture == null || !(videoDisplay.texture is RenderTexture))
            {
                RenderTexture rt = new RenderTexture(1920, 1080, 0);
                videoDisplay.texture = rt;
            }
            
            videoPlayer.targetTexture = videoDisplay.texture as RenderTexture;
        }

        // Setup audio
        if (cutsceneAudio == null)
        {
            cutsceneAudio = gameObject.AddComponent<AudioSource>();
        }
        
        videoPlayer.SetTargetAudioSource(0, cutsceneAudio);
        videoPlayer.loopPointReached += OnVideoEnd;
    }

    void HideAllScreens()
    {
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);
        
        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);
    }

    // ==================== PUBLIC METHODS ====================

    public void PlaySuccessCutscene()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only Master Client can trigger success cutscene!");
            return;
        }

        if (successCutscene == null)
        {
            Debug.LogError("Success cutscene video not assigned! Skipping to post-cutscene logic...");
            OnSuccessCutsceneEnd();
            return;
        }

        photonView.RPC("RPC_PlayCutscene", RpcTarget.All, (int)CutsceneType.Success);
    }

    public void PlayCaughtCutscene()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only Master Client can trigger caught cutscene!");
            return;
        }

        GameLog.Log("<color=yellow>[CutsceneManager] PlayCaughtCutscene() called!</color>");

        if (caughtCutscene == null)
        {
            Debug.LogWarning("Caught cutscene video not assigned! Skipping directly to Game Over...");
            OnCaughtCutsceneEnd(); // Skip to game over
            return;
        }

        photonView.RPC("RPC_PlayCutscene", RpcTarget.All, (int)CutsceneType.Caught);
    }

    // ==================== CUTSCENE PLAYBACK ====================

    [PunRPC]
    void RPC_PlayCutscene(int cutsceneTypeInt)
    {
        currentCutsceneType = (CutsceneType)cutsceneTypeInt;
        StartCoroutine(PlayCutsceneCoroutine());
    }

    IEnumerator PlayCutsceneCoroutine()
    {
        isCutscenePlaying = true;

        // Freeze players
        FreezeLocalPlayer(true);

        // Show cutscene canvas
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(true);

        // Load appropriate video
        VideoClip clipToPlay = (currentCutsceneType == CutsceneType.Success) ? successCutscene : caughtCutscene;
        
        if (clipToPlay == null)
        {
            Debug.LogError($"Video clip for {currentCutsceneType} is null!");
            OnVideoEnd(videoPlayer);
            yield break;
        }

        videoPlayer.clip = clipToPlay;
        videoPlayer.Prepare();

        GameLog.Log($"<color=cyan>[Cutscene] Preparing {currentCutsceneType} cutscene...</color>");

        // Wait for video to be prepared
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        GameLog.Log($"<color=green>[Cutscene] Playing {currentCutsceneType} cutscene!</color>");

        // Play video
        videoPlayer.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        GameLog.Log($"<color=yellow>[Cutscene] {currentCutsceneType} cutscene ended</color>");

        isCutscenePlaying = false;

        // Hide cutscene canvas
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);

        // Handle post-cutscene logic
        if (currentCutsceneType == CutsceneType.Success)
        {
            OnSuccessCutsceneEnd();
        }
        else if (currentCutsceneType == CutsceneType.Caught)
        {
            OnCaughtCutsceneEnd();
        }
    }

    // ==================== POST-CUTSCENE LOGIC ====================

    void OnSuccessCutsceneEnd()
    {
        GameLog.Log("<color=green>[Cutscene] Success cutscene complete - Activating guard patrol!</color>");

        // Unfreeze players
        FreezeLocalPlayer(false);

        // Notify guard to go to control room
        if (PhotonNetwork.IsMasterClient)
        {
            SecurityGuardNPC guard = FindObjectOfType<SecurityGuardNPC>();
            if (guard != null)
            {
                guard.ActivatePatrolMode();
            }
            else
            {
                Debug.LogWarning("SecurityGuardNPC not found in scene!");
            }
        }
    }

    void OnCaughtCutsceneEnd()
    {
        GameLog.Log("<color=red>[Cutscene] Caught cutscene complete - Showing Game Over!</color>");

        // Keep players frozen
        FreezeLocalPlayer(true);

        // ✅ ENHANCED: Show game over screen with better debugging
        if (gameOverScreen != null)
        {
            GameLog.Log($"<color=yellow>[Cutscene] Activating Game Over screen: {gameOverScreen.name}</color>");
            GameLog.Log($"   - Before activation: {gameOverScreen.activeSelf}");
            
            gameOverScreen.SetActive(true);
            
            GameLog.Log($"   - After activation: {gameOverScreen.activeSelf}");
            GameLog.Log($"   - Active in hierarchy: {gameOverScreen.activeInHierarchy}");
            
            // ✅ Force canvas update
            Canvas canvas = gameOverScreen.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
                canvas.enabled = true;
                GameLog.Log($"   - Canvas refreshed: {canvas.name}");
            }
            
            // ✅ Check CanvasGroup blocking
            CanvasGroup cg = gameOverScreen.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                GameLog.Log($"   - CanvasGroup alpha: {cg.alpha}");
                if (cg.alpha < 1f)
                {
                    Debug.LogWarning("   - ⚠️ CanvasGroup alpha was low, setting to 1");
                    cg.alpha = 1f;
                }
            }
        }
        else
        {
            Debug.LogError("<color=red>[Cutscene] ❌ Game Over screen is NULL! Cannot show!</color>");
        }

        // Show cursor for restart button
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        GameLog.Log("[Cutscene] Cursor unlocked for restart button");
    }

    // ==================== PLAYER CONTROL ====================

    void FreezeLocalPlayer(bool freeze)
    {
        GameObject localPlayer = PhotonNetwork.LocalPlayer.TagObject as GameObject;
        if (localPlayer == null) return;

        var characterController = localPlayer.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = !freeze;

        var tpc = localPlayer.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null)
            tpc.enabled = !freeze;

        var customTpc = localPlayer.GetComponent<CustomCharacterController.ThirdPersonController>();
        if (customTpc != null)
            customTpc.enabled = !freeze;

        if (freeze)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        GameLog.Log($"[Cutscene] Local player {(freeze ? "FROZEN" : "UNFROZEN")}");
    }

    // ==================== RESTART LEVEL ====================

    void RestartLevel()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            GameLog.Log("Only Master Client can restart level!");
            return;
        }

        GameLog.Log("<color=yellow>[Game] Restarting Level 3...</color>");
        PhotonNetwork.LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public bool IsCutscenePlaying()
    {
        return isCutscenePlaying;
    }
}