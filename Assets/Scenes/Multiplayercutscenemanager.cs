using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using Photon.Pun;

public class MultiplayerCutsceneManager : MonoBehaviourPunCallbacks
{
    [Header("Video References")]
    public VideoPlayer videoPlayer;
    public RawImage displayImage;
    public VideoClip videoClip;
    
    [Header("Scene Transition")]
    public string nextSceneName = "Level1";
    public float sceneTransitionDelay = 1f;
    
    [Header("Skip Settings")]
    [Tooltip("Both players must agree to skip")]
    public bool requireBothPlayersToSkip = true;
    public KeyCode skipKey = KeyCode.Space;
    public TMP_Text skipText;
    public string skipMessage = "Press SPACE to skip";
    public string waitingMessage = "Waiting for other player...";
    
    [Header("Fade Settings")]
    public bool fadeInAtStart = true;
    public bool fadeOutAtEnd = true;
    public float fadeDuration = 1f;
    public Image fadePanel;
    
    [Header("Audio Settings")]
    public VideoAudioOutputMode audioMode = VideoAudioOutputMode.Direct;
    public AudioSource audioSource;
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Private variables
    private RenderTexture renderTexture;
    private bool isVideoPlaying = false;
    private bool hasVideoEnded = false;
    private bool localPlayerWantsToSkip = false;
    private bool remotePlayerWantsToSkip = false;
    private CanvasGroup skipTextCanvasGroup;
    
    void Start()
    {
        SetupCutscene();
        
        // Only master client controls video timing
        if (PhotonNetwork.IsMasterClient)
        {
            PlayCutscene();
        }
        else
        {
            // Non-master waits for master
            if (fadeInAtStart && fadePanel != null)
            {
                StartCoroutine(FadeIn());
            }
            videoPlayer.Play();
            isVideoPlaying = true;
        }
    }
    
    void SetupCutscene()
    {
        if (videoPlayer == null || displayImage == null)
        {
            Debug.LogError("MultiplayerCutsceneManager: Missing required components!");
            enabled = false;
            return;
        }
        
        // Create render texture
        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();
        
        // Setup video player
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.isLooping = false;
        
        // Setup audio
        videoPlayer.audioOutputMode = audioMode;
        if (audioMode == VideoAudioOutputMode.AudioSource && audioSource != null)
        {
            videoPlayer.SetTargetAudioSource(0, audioSource);
            audioSource.volume = volume;
        }
        else if (audioMode == VideoAudioOutputMode.Direct)
        {
            videoPlayer.SetDirectAudioVolume(0, volume);
        }
        
        // Assign render texture
        displayImage.texture = renderTexture;
        
        // Setup skip text
        if (skipText != null)
        {
            skipText.text = skipMessage;
            skipText.gameObject.SetActive(true);
            
            skipTextCanvasGroup = skipText.GetComponent<CanvasGroup>();
            if (skipTextCanvasGroup == null)
            {
                skipTextCanvasGroup = skipText.gameObject.AddComponent<CanvasGroup>();
            }
            StartCoroutine(PulseSkipText());
        }
        
        // Setup fade panel
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            fadePanel.color = new Color(0, 0, 0, fadeInAtStart ? 1 : 0);
        }
        
        // Subscribe to video end event (master client only)
        if (PhotonNetwork.IsMasterClient)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
        }
        
        if (showDebugLogs)
            Debug.Log($"MultiplayerCutsceneManager: Setup complete (IsMaster: {PhotonNetwork.IsMasterClient})");
    }
    
    void PlayCutscene()
    {
        if (videoPlayer == null) return;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Master client starting cutscene");
        
        if (fadeInAtStart && fadePanel != null)
        {
            StartCoroutine(FadeIn());
        }
        
        videoPlayer.Play();
        isVideoPlaying = true;
    }
    
    void Update()
    {
        if (!isVideoPlaying || hasVideoEnded) return;
        
        // Skip input
        if (Input.GetKeyDown(skipKey) && !localPlayerWantsToSkip)
        {
            OnLocalPlayerWantsToSkip();
        }
        
        // Check if video ended naturally (master client only)
        if (PhotonNetwork.IsMasterClient && !videoPlayer.isPlaying && videoPlayer.frame > 0)
        {
            OnVideoFinished(videoPlayer);
        }
    }
    
    void OnLocalPlayerWantsToSkip()
    {
        localPlayerWantsToSkip = true;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Local player wants to skip");
        
        if (requireBothPlayersToSkip)
        {
            // Notify other player
            photonView.RPC("RPC_PlayerWantsToSkip", RpcTarget.Others);
            
            // Update UI
            if (skipText != null)
            {
                skipText.text = waitingMessage;
            }
            
            // Check if both players want to skip
            if (remotePlayerWantsToSkip)
            {
                // Both players agree, skip!
                if (PhotonNetwork.IsMasterClient)
                {
                    SkipCutscene();
                }
            }
        }
        else
        {
            // Single player can skip, notify everyone
            if (PhotonNetwork.IsMasterClient)
            {
                SkipCutscene();
            }
            else
            {
                photonView.RPC("RPC_SkipCutscene", RpcTarget.MasterClient);
            }
        }
    }
    
    [PunRPC]
    void RPC_PlayerWantsToSkip()
    {
        remotePlayerWantsToSkip = true;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Remote player wants to skip");
        
        // Check if both players want to skip
        if (localPlayerWantsToSkip && PhotonNetwork.IsMasterClient)
        {
            SkipCutscene();
        }
    }
    
    [PunRPC]
    void RPC_SkipCutscene()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SkipCutscene();
        }
    }
    
    void SkipCutscene()
    {
        if (!PhotonNetwork.IsMasterClient || hasVideoEnded) return;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Skipping cutscene for all players");
        
        // Notify all players to skip
        photonView.RPC("RPC_NotifySkip", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_NotifySkip()
    {
        if (hasVideoEnded) return;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Received skip notification");
        
        videoPlayer.Stop();
        isVideoPlaying = false;
        hasVideoEnded = true;
        
        StartCoroutine(TransitionToNextScene());
    }
    
    void OnVideoFinished(VideoPlayer vp)
    {
        if (hasVideoEnded || !PhotonNetwork.IsMasterClient) return;
        
        hasVideoEnded = true;
        
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Video finished, notifying all players");
        
        // Notify all players
        photonView.RPC("RPC_NotifyVideoEnd", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_NotifyVideoEnd()
    {
        if (showDebugLogs)
            Debug.Log("MultiplayerCutsceneManager: Received video end notification");
        
        isVideoPlaying = false;
        hasVideoEnded = true;
        
        StartCoroutine(TransitionToNextScene());
    }
    
    IEnumerator TransitionToNextScene()
    {
        // Hide skip text
        if (skipText != null)
        {
            skipText.gameObject.SetActive(false);
        }
        
        // Wait for delay
        if (sceneTransitionDelay > 0)
        {
            yield return new WaitForSeconds(sceneTransitionDelay);
        }
        
        // Fade out
        if (fadeOutAtEnd && fadePanel != null)
        {
            yield return StartCoroutine(FadeOut());
        }
        
        // Load next scene (master client only)
        if (PhotonNetwork.IsMasterClient && !string.IsNullOrEmpty(nextSceneName))
        {
            if (showDebugLogs)
                Debug.Log($"MultiplayerCutsceneManager: Loading scene '{nextSceneName}'");
            
            PhotonNetwork.LoadLevel(nextSceneName);
        }
    }
    
    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color color = fadePanel.color;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadePanel.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        
        fadePanel.color = new Color(color.r, color.g, color.b, 0f);
    }
    
    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color color = fadePanel.color;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            fadePanel.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        
        fadePanel.color = new Color(color.r, color.g, color.b, 1f);
    }
    
    IEnumerator PulseSkipText()
    {
        if (skipTextCanvasGroup == null) yield break;
        
        while (isVideoPlaying && !hasVideoEnded)
        {
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                skipTextCanvasGroup.alpha = Mathf.Lerp(1f, 0.3f, elapsed);
                yield return null;
            }
            
            elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                skipTextCanvasGroup.alpha = Mathf.Lerp(0.3f, 1f, elapsed);
                yield return null;
            }
        }
    }
    
    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.Stop();
        }
        
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}