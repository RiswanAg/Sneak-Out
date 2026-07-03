using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class CutsceneManager : MonoBehaviour
{
    [Header("Video References")]
    [Tooltip("The Video Player component")]
    public VideoPlayer videoPlayer;
    
    [Tooltip("UI RawImage to display video")]
    public RawImage displayImage;
    
    [Tooltip("MP4 video clip to play")]
    public VideoClip videoClip;
    
    [Header("Scene Transition")]
    [Tooltip("Scene to load after cutscene ends")]
    public string nextSceneName = "MainMenu";
    
    [Tooltip("Delay before loading next scene (seconds)")]
    public float sceneTransitionDelay = 1f;
    
    [Header("Skip Settings")]
    [Tooltip("Allow player to skip cutscene")]
    public bool allowSkip = true;
    
    [Tooltip("Key to skip cutscene")]
    public KeyCode skipKey = KeyCode.Space;
    
    [Tooltip("Show skip instruction text")]
    public bool showSkipText = true;
    
    [Tooltip("Skip instruction UI Text")]
    public TMP_Text skipText;
    
    [Tooltip("Skip text message")]
    public string skipMessage = "Press SPACE to skip";
    
    [Header("Fade Settings")]
    [Tooltip("Fade in at start")]
    public bool fadeInAtStart = true;
    
    [Tooltip("Fade out before scene transition")]
    public bool fadeOutAtEnd = true;
    
    [Tooltip("Fade duration (seconds)")]
    public float fadeDuration = 1f;
    
    [Tooltip("Fade panel (black image)")]
    public Image fadePanel;
    
    [Header("Audio Settings")]
    [Tooltip("Audio output mode")]
    public VideoAudioOutputMode audioMode = VideoAudioOutputMode.Direct;
    
    [Tooltip("Audio Source (if using AudioSource mode)")]
    public AudioSource audioSource;
    
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Private variables
    private RenderTexture renderTexture;
    private bool isVideoPlaying = false;
    private bool hasVideoEnded = false;
    private CanvasGroup skipTextCanvasGroup;
    
    void Start()
    {
        SetupCutscene();
        PlayCutscene();
    }
    
    void SetupCutscene()
    {
        // Validate components
        if (videoPlayer == null)
        {
            Debug.LogError("CutsceneManager: VideoPlayer is not assigned!");
            enabled = false;
            return;
        }
        
        if (displayImage == null)
        {
            Debug.LogError("CutsceneManager: Display RawImage is not assigned!");
            enabled = false;
            return;
        }
        
        // Create render texture for video
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
        
        // Assign render texture to UI
        displayImage.texture = renderTexture;
        
        // Setup skip text
        if (showSkipText && skipText != null)
        {
            skipText.text = skipMessage;
            skipText.gameObject.SetActive(allowSkip);
            
            // Add fade effect to skip text
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
        
        // Subscribe to video end event
        videoPlayer.loopPointReached += OnVideoFinished;
        
        if (showDebugLogs)
            Debug.Log("CutsceneManager: Setup complete");
    }
    
    void PlayCutscene()
    {
        if (videoPlayer == null) return;
        
        if (showDebugLogs)
            Debug.Log("CutsceneManager: Playing cutscene");
        
        // Fade in if enabled
        if (fadeInAtStart && fadePanel != null)
        {
            StartCoroutine(FadeIn());
        }
        
        // Play video
        videoPlayer.Play();
        isVideoPlaying = true;
    }
    
    void Update()
    {
        // Skip functionality
        if (allowSkip && Input.GetKeyDown(skipKey) && isVideoPlaying && !hasVideoEnded)
        {
            SkipCutscene();
        }
        
        // Check if video is still playing
        if (isVideoPlaying && !videoPlayer.isPlaying && videoPlayer.frame > 0 && !hasVideoEnded)
        {
            // Video ended naturally
            OnVideoFinished(videoPlayer);
        }
    }
    
    void SkipCutscene()
    {
        if (showDebugLogs)
            Debug.Log("CutsceneManager: Cutscene skipped by player");
        
        // Stop video
        videoPlayer.Stop();
        isVideoPlaying = false;
        hasVideoEnded = true;
        
        // Load next scene
        StartCoroutine(TransitionToNextScene());
    }
    
    void OnVideoFinished(VideoPlayer vp)
    {
        if (hasVideoEnded) return;
        
        hasVideoEnded = true;
        isVideoPlaying = false;
        
        if (showDebugLogs)
            Debug.Log("CutsceneManager: Video finished playing");
        
        // Transition to next scene
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
        
        // Fade out if enabled
        if (fadeOutAtEnd && fadePanel != null)
        {
            yield return StartCoroutine(FadeOut());
        }
        
        // Load next scene
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (showDebugLogs)
                Debug.Log($"CutsceneManager: Loading scene '{nextSceneName}'");
            
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("CutsceneManager: Next scene name is not set!");
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
            // Fade out
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                skipTextCanvasGroup.alpha = Mathf.Lerp(1f, 0.3f, elapsed);
                yield return null;
            }
            
            // Fade in
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
        // Cleanup
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