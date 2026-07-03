using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Level3VictoryCutsceneManager.cs - Victory cutscene when players escape
/// Shows final cutscene, then displays victory UI
/// </summary>
public class Level3VictoryCutsceneManager : MonoBehaviourPun
{
    public static Level3VictoryCutsceneManager Instance { get; private set; }

    [Header("Victory Cutscene")]
    [Tooltip("Victory cutscene video (players escaping school)")]
    public VideoClip victoryCutscene;

    [Header("UI References")]
    public GameObject cutsceneCanvas;
    public RawImage videoDisplay;
    public VideoPlayer videoPlayer;

    [Header("Audio")]
    public AudioSource cutsceneAudio;

    // State
    private bool isCutscenePlaying = false;

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
        HideCutscene();
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
            // Create RenderTexture if it doesn't exist
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

    void HideCutscene()
    {
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);
    }

    /// <summary>
    /// Play victory cutscene (only Master Client calls this)
    /// </summary>
    public void PlayVictoryCutscene()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[Victory] Only Master Client can trigger victory cutscene!");
            return;
        }

        if (victoryCutscene == null)
        {
            Debug.LogError("[Victory] Victory cutscene video not assigned! Skipping to victory UI...");
            OnVictoryCutsceneEnd(); // Skip to victory UI
            return;
        }

        photonView.RPC("RPC_PlayVictoryCutscene", RpcTarget.All);
    }

    [PunRPC]
    void RPC_PlayVictoryCutscene()
    {
        StartCoroutine(PlayCutsceneCoroutine());
    }

    IEnumerator PlayCutsceneCoroutine()
    {
        isCutscenePlaying = true;

        Debug.Log("<color=cyan>[Victory] Playing victory cutscene...</color>");

        // Freeze players
        FreezeLocalPlayer(true);

        // Show cutscene canvas
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(true);

        // Load video
        videoPlayer.clip = victoryCutscene;
        videoPlayer.Prepare();

        // Wait for video to be prepared
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        Debug.Log("<color=green>[Victory] Cutscene prepared, playing now!</color>");

        // Play video (synchronized for all players)
        videoPlayer.Play();

        // Wait for video to finish (handled by OnVideoEnd callback)
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("<color=yellow>[Victory] Cutscene ended</color>");

        isCutscenePlaying = false;

        // Hide cutscene canvas
        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);

        // Show victory UI
        OnVictoryCutsceneEnd();
    }

    void OnVictoryCutsceneEnd()
    {
        Debug.Log("<color=green>[Victory] Victory cutscene complete - Showing victory UI!</color>");

        // Keep players frozen (they won, game over)
        FreezeLocalPlayer(true);

        // Show victory UI
        Level3VictoryUI victoryUI = FindObjectOfType<Level3VictoryUI>();
        if (victoryUI != null)
        {
            victoryUI.ShowVictoryScreen();
        }
        else
        {
            Debug.LogError("[Victory] Level3VictoryUI not found in scene!");
        }

        // Show cursor for UI interaction
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void FreezeLocalPlayer(bool freeze)
    {
        GameObject localPlayer = PhotonNetwork.LocalPlayer.TagObject as GameObject;
        if (localPlayer == null) return;

        // Disable movement controllers
        var characterController = localPlayer.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = !freeze;

        var tpc = localPlayer.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null)
            tpc.enabled = !freeze;

        var customTpc = localPlayer.GetComponent<CustomCharacterController.ThirdPersonController>();
        if (customTpc != null)
            customTpc.enabled = !freeze;

        Debug.Log($"[Victory] Local player {(freeze ? "FROZEN" : "UNFROZEN")}");
    }

    public bool IsCutscenePlaying()
    {
        return isCutscenePlaying;
    }
}