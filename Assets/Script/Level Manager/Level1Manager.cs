using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages individual player game overs and win conditions for Level 1
/// UPDATED: Transitions to cutscene scene after victory
/// </summary>
public class Level1Manager : MonoBehaviourPunCallbacks
{
    public static Level1Manager Instance { get; private set; }
    
    [Header("Game Over Settings")]
    public GameObject individualGameOverPanel;
    public TMP_Text gameOverText;
    
    [Header("Buttons")]
    public Button retryButton;
    public Button mainMenuButton;
    
    [Header("Win Condition")]
    public Transform winCheckpoint;
    public float checkpointRadius = 3f;
    
    [Header("Victory Cutscene Settings")]
    [Tooltip("Show in-game cutscene object before transition (optional)")]
    public GameObject inGameCutsceneObject;
    
    [Tooltip("Cutscene scene to load after victory")]
    public string victoryCutsceneScene = "Level1VictoryCutscene";
    
    [Tooltip("Skip cutscene and go directly to next level")]
    public bool skipCutscene = false;
    
    [Tooltip("Next level scene (if skipping cutscene OR after cutscene ends)")]
    public string nextLevelScene = "Level 2";
    
    [Tooltip("Delay before loading cutscene/next level")]
    public float transitionDelay = 2f;
    
    [Header("Scene Names")]
    public string mainMenuScene = "Menu";
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip gameOverSound;
    public AudioClip victorySound;
    
    // Tracking
    private HashSet<GameObject> failedPlayers = new HashSet<GameObject>();
    private HashSet<int> playersReachedCheckpoint = new HashSet<int>();
    private bool levelComplete = false;
    private bool isRestarting = false;
    private bool isGoingToMenu = false;
    
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
        
        if (individualGameOverPanel != null)
            individualGameOverPanel.SetActive(false);
        
        if (inGameCutsceneObject != null)
            inGameCutsceneObject.SetActive(false);
        
        SetupButtons();
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
        if (levelComplete) return;
        CheckWinCondition();
    }
    
    public void PlayerFailed(GameObject player)
    {
        if (player == null) return;
        
        PhotonView pv = player.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine) return;
        
        if (failedPlayers.Contains(player)) return;
        
        Debug.Log($"[Level1] Player {player.name} FAILED!");
        
        failedPlayers.Add(player);
        FreezePlayer(player);
        ShowIndividualGameOver(player);
        
        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);
    }
    
    void FreezePlayer(GameObject player)
    {
        Debug.Log($"[Level1] Freezing player: {player.name}");
        
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        
        MonoBehaviour[] allScripts = player.GetComponents<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script == null) continue;
            
            string scriptName = script.GetType().Name;
            
            if (scriptName == "PhotonView" || 
                scriptName == "PhotonTransformView" ||
                scriptName == "PhotonAnimatorView")
                continue;
            
            if (scriptName.Contains("Controller") || 
                scriptName.Contains("Movement") || 
                scriptName.Contains("Input") ||
                scriptName.Contains("Player"))
            {
                script.enabled = false;
            }
        }
        
        var animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetFloat("Speed", 0);
            animator.SetBool("Grounded", true);
            animator.SetBool("Jump", false);
            animator.SetBool("FreeFall", false);
        }
        
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    void ShowIndividualGameOver(GameObject player)
    {
        if (individualGameOverPanel != null)
        {
            individualGameOverPanel.SetActive(true);
            
            if (gameOverText != null)
            {
                string playerName = PhotonNetwork.LocalPlayer.NickName;
                gameOverText.text = $"GAME OVER\n{playerName} was caught!";
            }
        }
    }
    
    public void PlayerReachedCheckpoint(int actorNumber)
    {
        if (levelComplete) return;
        
        if (!playersReachedCheckpoint.Contains(actorNumber))
        {
            playersReachedCheckpoint.Add(actorNumber);
            photonView.RPC("RPC_PlayerReachedCheckpoint", RpcTarget.AllBuffered, actorNumber);
        }
    }
    
    [PunRPC]
    void RPC_PlayerReachedCheckpoint(int actorNumber)
    {
        playersReachedCheckpoint.Add(actorNumber);
    }
    
    void CheckWinCondition()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
        
        if (playersReachedCheckpoint.Count >= 2)
        {
            TriggerVictory();
        }
    }
    
    void TriggerVictory()
    {
        if (levelComplete) return;
        levelComplete = true;
        
        Debug.Log("[Level1] ✅ VICTORY! Both players succeeded!");
        
        // Play victory sound
        if (audioSource != null && victorySound != null)
            audioSource.PlayOneShot(victorySound);
        
        // Show optional in-game cutscene object
        if (inGameCutsceneObject != null)
            inGameCutsceneObject.SetActive(true);
        
        // Master client loads next scene
        if (PhotonNetwork.IsMasterClient)
        {
            if (skipCutscene)
            {
                // Go directly to next level
                Invoke(nameof(LoadNextLevel), transitionDelay);
            }
            else
            {
                // Load cutscene scene
                Invoke(nameof(LoadCutsceneScene), transitionDelay);
            }
        }
    }
    
    /// <summary>
    /// ✅ Load the victory cutscene scene
    /// </summary>
    void LoadCutsceneScene()
    {
        Debug.Log($"[Level1] Loading cutscene: {victoryCutsceneScene}");
        
        // ✅ PhotonNetwork.LoadLevel syncs for all players
        PhotonNetwork.LoadLevel(victoryCutsceneScene);
    }
    
    /// <summary>
    /// ✅ Load next level (called if skipping cutscene)
    /// </summary>
    void LoadNextLevel()
    {
        Debug.Log($"[Level1] Loading next level: {nextLevelScene}");
        PhotonNetwork.LoadLevel(nextLevelScene);
    }
    
    // ==================== RESTART LEVEL ====================
    
    public void RestartLevel()
    {
        if (isRestarting) return;
        isRestarting = true;
        
        Debug.Log("[Level1] RestartLevel called!");
        
        Time.timeScale = 1f;
        
        if (individualGameOverPanel != null)
            individualGameOverPanel.SetActive(false);
        
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_RestartForEveryone", RpcTarget.All);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    
    [PunRPC]
    void RPC_RestartForEveryone()
    {
        Debug.Log("[Level1] RPC_RestartForEveryone received! Reloading scene...");
        
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (individualGameOverPanel != null)
            individualGameOverPanel.SetActive(false);
        
        CleanupBeforeRestart();
        StartCoroutine(DelayedSceneLoad());
    }
    
    void CleanupBeforeRestart()
    {
        Debug.Log("[Level1] Cleaning up before restart...");
        
        if (PhotonNetwork.LocalPlayer.TagObject != null)
        {
            Debug.Log("[Level1] Clearing TagObject reference");
            PhotonNetwork.LocalPlayer.TagObject = null;
        }
        
        // Reset any static NPC flags if you have them
        StudentNPC.ResetStaticFlags();
    }
    
    IEnumerator DelayedSceneLoad()
    {
        yield return null;
        yield return null;
        
        Debug.Log("[Level1] Loading scene now...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    // ==================== RETURN TO MENU ====================
    
    public void ReturnToMenu()
    {
        if (isGoingToMenu) return;
        isGoingToMenu = true;
        
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
        if (isGoingToMenu)
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (winCheckpoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(winCheckpoint.position, checkpointRadius);
        }
    }
}