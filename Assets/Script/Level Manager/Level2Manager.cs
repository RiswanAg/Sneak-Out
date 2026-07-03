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
    public string nextLevelScene = SceneNames.Level3;
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
    
    // State tracking
    private bool isGameOver = false;
    private bool isVictory = false;
    private bool isRestarting = false;
    private bool isGoingToMenu = false;
    private HashSet<int> playersAtCheckpoint = new HashSet<int>();
    
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
        
        // Hide panels
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
        if (cutsceneObject != null)
            cutsceneObject.SetActive(false);
        
        SetupButtons();
    }
    
    void Start()
    {
        // Find Cikgu if not assigned
        if (cikguNPC == null)
        {
            cikguNPC = FindObjectOfType<CikguNPC>();
        }
        
        // Subscribe to Cikgu's catch event
        CikguNPC.OnPlayerCaught += OnPlayerCaughtByCikgu;
        
        // Reset flags on scene start
        isGameOver = false;
        isVictory = false;
        isRestarting = false;
        
        Debug.Log("[Level2] Level 2 Manager initialized - Team Game Over Mode");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        CikguNPC.OnPlayerCaught -= OnPlayerCaughtByCikgu;
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
    }
    
    // ==================== GAME OVER (Cikgu Catches Player) ====================
    
    /// <summary>
    /// Called when Cikgu catches ANY player
    /// </summary>
    void OnPlayerCaughtByCikgu(GameObject caughtPlayer)
    {
        if (isGameOver || isVictory || isRestarting) return;
        
        Debug.Log($"[Level2] Player caught by Cikgu: {caughtPlayer?.name}");
        
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
        
        Debug.Log($"[Level2] TEAM GAME OVER! {caughtPlayerName} was caught!");
        
        // Freeze ALL players
        FreezeAllPlayers();
        
        // Show game over UI
        ShowGameOverUI(caughtPlayerName);
        
        // Play sound
        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);
        
        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
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
        
        Debug.Log($"[Level2] Freezing player: {player.name}");
        
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
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            
            if (gameOverText != null)
            {
                gameOverText.text = "TERTANGKAP!";
            }
            
            if (gameOverSubtext != null)
            {
                gameOverSubtext.text = $"{caughtPlayerName} telah ditangkap Cikgu!\nKedua-dua pemain kalah.";
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
            Debug.Log($"[Level2] Player {actorNumber} at checkpoint ({playersAtCheckpoint.Count}/{requiredPlayersAtCheckpoint})");
        }
        else
        {
            playersAtCheckpoint.Remove(actorNumber);
            Debug.Log($"[Level2] Player {actorNumber} left checkpoint ({playersAtCheckpoint.Count}/{requiredPlayersAtCheckpoint})");
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
        
        Debug.Log("[Level2] VICTORY! All players reached checkpoint!");
        
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
        Debug.Log($"[Level2] Loading next level: {nextLevelScene}");
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
        
        Debug.Log("[Level2] RestartLevel called!");
        
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
    /// ✅ This RPC is received by ALL players and each one reloads their scene
    /// </summary>
    [PunRPC]
    void RPC_RestartForEveryone()
    {
        Debug.Log("[Level2] RPC_RestartForEveryone received! Reloading scene...");
        
        // Reset state
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Hide UI
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
        
        // ✅ Clean up before loading new scene
        CleanupBeforeRestart();
        
        // ✅ Use coroutine to ensure cleanup happens before scene load
        StartCoroutine(DelayedSceneLoad());
    }
    
    void CleanupBeforeRestart()
    {
        Debug.Log("[Level2] Cleaning up before restart...");
        
        // Just clear the TagObject reference
        if (PhotonNetwork.LocalPlayer.TagObject != null)
        {
            Debug.Log("[Level2] Clearing TagObject reference");
            PhotonNetwork.LocalPlayer.TagObject = null;
        }
        
        // Reset local state
        isGameOver = false;
        isVictory = false;
        playersAtCheckpoint.Clear();
    }
    
    IEnumerator DelayedSceneLoad()
    {
        // Wait a frame for cleanup to complete
        yield return null;
        yield return null;
        
        Debug.Log("[Level2] Loading scene now...");
        
        // ✅ Each player loads the scene themselves
        // This ensures both players reload properly
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    // ==================== RETURN TO MENU ====================
    
    public void ReturnToMenu()
    {
        if (isGoingToMenu) return;
        isGoingToMenu = true;
        
        Debug.Log("[Level2] Returning to menu...");
        
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