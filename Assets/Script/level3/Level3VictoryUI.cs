using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;

/// <summary>
/// Level3VictoryUI.cs - Victory screen after completing Level 3
/// Shows congratulations message with Restart and Menu buttons
/// </summary>
public class Level3VictoryUI : MonoBehaviourPun
{
    [Header("UI References")]
    public GameObject victoryPanel;
    
    [Header("Text")]
    public TMPro.TMP_Text victoryTitleText;
    public TMPro.TMP_Text victoryMessageText;
    
    [Header("Buttons")]
    public Button restartButton;
    public Button menuButton;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip victoryMusic;
    
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;
    
    [Header("Animation (Optional)")]
    public Animator victoryAnimator;
    
    void Start()
    {
        // Hide victory panel initially
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
        
        // Setup buttons
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        
        if (menuButton != null)
            menuButton.onClick.AddListener(OnMenuClicked);
        
        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = musicVolume;
    }
    
    /// <summary>
    /// Show the victory screen
    /// </summary>
    public void ShowVictoryScreen()
    {
        Debug.Log("<color=green>[VictoryUI] Showing victory screen!</color>");
        
        // Show panel
        if (victoryPanel != null)
            victoryPanel.SetActive(true);
        
        // Set victory text
        if (victoryTitleText != null)
            victoryTitleText.text = "TAHNIAH!";
        
        if (victoryMessageText != null)
            victoryMessageText.text = "Anda berjaya melarikan diri dari asrama!\nSelamat malam!";
        
        // Play victory music
        if (victoryMusic != null && audioSource != null)
        {
            audioSource.clip = victoryMusic;
            audioSource.Play();
        }
        
        // Play animation
        if (victoryAnimator != null)
        {
            victoryAnimator.SetTrigger("Show");
        }
        
        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    /// <summary>
    /// Hide the victory screen
    /// </summary>
    public void HideVictoryScreen()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
        
        // Stop music
        if (audioSource != null)
            audioSource.Stop();
    }
    
    /// <summary>
    /// Restart Level 3
    /// </summary>
    void OnRestartClicked()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[VictoryUI] Only Master Client can restart!");
            return;
        }
        
        Debug.Log("<color=yellow>[VictoryUI] Restarting Level 3...</color>");
        
        // Stop music
        if (audioSource != null)
            audioSource.Stop();
        
        // Reload Level 3
        PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Return to main menu
    /// </summary>
    void OnMenuClicked()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[VictoryUI] Only Master Client can return to menu!");
            return;
        }
        
        Debug.Log("<color=yellow>[VictoryUI] Returning to menu...</color>");
        
        // Stop music
        if (audioSource != null)
            audioSource.Stop();
        
        // Disconnect from room
        PhotonNetwork.LeaveRoom();
        
        // Load menu scene (adjust scene name if needed)
        PhotonNetwork.LoadLevel("MainMenu");
    }
    
    void OnDestroy()
    {
        // Clean up button listeners
        if (restartButton != null)
            restartButton.onClick.RemoveListener(OnRestartClicked);
        
        if (menuButton != null)
            menuButton.onClick.RemoveListener(OnMenuClicked);
    }
}
