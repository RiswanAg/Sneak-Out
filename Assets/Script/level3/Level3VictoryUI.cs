using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;

/// <summary>
/// Level3VictoryUI.cs - Victory screen after completing Level 3
/// Shows congratulations message with Restart and Menu buttons
/// </summary>
public class Level3VictoryUI : MonoBehaviourPunCallbacks
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
        GameLog.Log("<color=green>[VictoryUI] Showing victory screen!</color>");
        
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
        CursorManager.SetFree();
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
    /// Restart Level 3 — either player can click; the Master Client reloads for everyone
    /// </summary>
    void OnRestartClicked()
    {
        GameLog.Log("<color=yellow>[VictoryUI] Restart requested...</color>");

        // Stop music
        if (audioSource != null)
            audioSource.Stop();

        // Broadcast so every client can clear its own player before the reload;
        // only the master performs the actual (synced) scene load.
        photonView.RPC("RPC_RequestRestart", RpcTarget.All);
    }

    [PunRPC]
    void RPC_RequestRestart()
    {
        GameLog.Log("<color=yellow>[VictoryUI] Restart received...</color>");

        // Clear our stale player reference. Do NOT PhotonNetwork.Destroy here - it
        // races with the master's LoadLevel and throws "Destroy Failed. Could not
        // find PhotonView...". The reload destroys players; they respawn on load.
        PhotonNetwork.LocalPlayer.TagObject = null;

        // EVERY client reloads itself. LoadLevel always loads locally and pauses
        // this client's queue during the load, so both players restart and neither
        // loses the partner's spawn. (A master-only LoadLevel does NOT reload
        // clients on a same-scene restart - the synced scene property is unchanged.)
        PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Return to main menu — either player can click; both players leave the room
    /// </summary>
    void OnMenuClicked()
    {
        GameLog.Log("<color=yellow>[VictoryUI] Menu requested...</color>");
        photonView.RPC("RPC_ReturnToMenu", RpcTarget.All);
    }

    [PunRPC]
    void RPC_ReturnToMenu()
    {
        GameLog.Log("<color=yellow>[VictoryUI] Returning to menu...</color>");

        // Stop music
        if (audioSource != null)
            audioSource.Stop();

        // Disconnect from room; scene loads once OnLeftRoom fires below
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(SceneNames.MainMenu);
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
