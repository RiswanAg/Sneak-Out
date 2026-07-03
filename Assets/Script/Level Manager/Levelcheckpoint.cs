using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Checkpoint that requires ALL players to be inside before proceeding to next level
/// Clears inventory when transitioning
/// </summary>
public class LevelCheckpoint : MonoBehaviourPunCallbacks
{
    [Header("Checkpoint Settings")]
    [Tooltip("Number of players required (usually 2)")]
    public int requiredPlayers = 2;
    
    [Tooltip("Time to wait after all players arrive before loading")]
    public float transitionDelay = 3f;
    
    [Tooltip("Next level scene name")]
    public string nextLevelScene = SceneNames.Level2;
    
    [Header("UI References")]
    [Tooltip("Panel to show checkpoint status")]
    public GameObject checkpointUI;
    
    [Tooltip("Text showing waiting status")]
    public TMP_Text statusText;
    
    [Header("Visual Feedback")]
    [Tooltip("Optional: Object to activate when checkpoint is active")]
    public GameObject checkpointActiveVisual;
    
    [Tooltip("Optional: Object to activate when all players ready")]
    public GameObject allReadyVisual;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip playerEnteredSound;
    public AudioClip allReadySound;
    public AudioClip transitionSound;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Tracking
    private bool localPlayerInCheckpoint = false;
    private bool isTransitioning = false;
    private int playersInCheckpoint = 0;
    
    // Property key for syncing
    private const string CHECKPOINT_KEY = "InCheckpoint";
    
    void Start()
    {
        // Hide UI at start
        if (checkpointUI != null)
            checkpointUI.SetActive(false);
        
        if (checkpointActiveVisual != null)
            checkpointActiveVisual.SetActive(false);
        
        if (allReadyVisual != null)
            allReadyVisual.SetActive(false);
        
        // Reset checkpoint property for local player
        if (PhotonNetwork.LocalPlayer != null)
        {
            Hashtable props = new Hashtable { { CHECKPOINT_KEY, false } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
        
        if (showDebugLogs)
            Debug.Log($"[Checkpoint] Initialized. Waiting for {requiredPlayers} players. Next level: {nextLevelScene}");
    }
    
    void Update()
    {
        if (isTransitioning) return;
        
        // Count players in checkpoint
        UpdatePlayerCount();
        
        // Update UI
        UpdateUI();
    }
    
    void UpdatePlayerCount()
    {
        playersInCheckpoint = 0;
        
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.TryGetValue(CHECKPOINT_KEY, out object inCheckpoint))
            {
                if ((bool)inCheckpoint)
                {
                    playersInCheckpoint++;
                }
            }
        }
    }
    
    void UpdateUI()
    {
        // Only show UI if local player is in checkpoint
        if (!localPlayerInCheckpoint)
        {
            if (checkpointUI != null)
                checkpointUI.SetActive(false);
            return;
        }
        
        if (checkpointUI != null)
            checkpointUI.SetActive(true);
        
        if (statusText != null)
        {
            if (playersInCheckpoint >= requiredPlayers)
            {
                statusText.text = "All players ready!\nProceeding to next level...";
            }
            else
            {
                int waiting = requiredPlayers - playersInCheckpoint;
                statusText.text = $"Waiting for other player...\n({playersInCheckpoint}/{requiredPlayers})";
            }
        }
        
        // Visual feedback
        if (allReadyVisual != null)
            allReadyVisual.SetActive(playersInCheckpoint >= requiredPlayers);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isTransitioning) return;
        
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            
            // Only handle local player
            if (pv != null && pv.IsMine)
            {
                if (!localPlayerInCheckpoint)
                {
                    localPlayerInCheckpoint = true;
                    
                    // Sync to network
                    Hashtable props = new Hashtable { { CHECKPOINT_KEY, true } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    
                    if (showDebugLogs)
                        Debug.Log($"[Checkpoint] Local player ENTERED checkpoint");
                    
                    // Play sound
                    if (audioSource != null && playerEnteredSound != null)
                        audioSource.PlayOneShot(playerEnteredSound);
                    
                    // Show visual
                    if (checkpointActiveVisual != null)
                        checkpointActiveVisual.SetActive(true);
                    
                    // Check if all players ready
                    CheckAllPlayersReady();
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (isTransitioning) return;
        
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            
            // Only handle local player
            if (pv != null && pv.IsMine)
            {
                if (localPlayerInCheckpoint)
                {
                    localPlayerInCheckpoint = false;
                    
                    // Sync to network
                    Hashtable props = new Hashtable { { CHECKPOINT_KEY, false } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    
                    if (showDebugLogs)
                        Debug.Log($"[Checkpoint] Local player LEFT checkpoint");
                    
                    // Hide visual
                    if (checkpointActiveVisual != null)
                        checkpointActiveVisual.SetActive(false);
                }
            }
        }
    }
    
    // Called when any player's properties change
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(CHECKPOINT_KEY))
        {
            if (showDebugLogs)
                Debug.Log($"[Checkpoint] Player {targetPlayer.NickName} checkpoint status changed");
            
            CheckAllPlayersReady();
        }
    }
    
    void CheckAllPlayersReady()
    {
        if (isTransitioning) return;
        
        UpdatePlayerCount();
        
        if (showDebugLogs)
            Debug.Log($"[Checkpoint] Players in checkpoint: {playersInCheckpoint}/{requiredPlayers}");
        
        if (playersInCheckpoint >= requiredPlayers)
        {
            if (showDebugLogs)
                Debug.Log("[Checkpoint] ✅ ALL PLAYERS READY! Starting transition...");
            
            // Play ready sound
            if (audioSource != null && allReadySound != null)
                audioSource.PlayOneShot(allReadySound);
            
            // Start transition (only master client initiates)
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(TransitionToNextLevel());
            }
        }
    }
    
    IEnumerator TransitionToNextLevel()
    {
        isTransitioning = true;
        
        // Notify all players that transition is starting
        photonView.RPC("RPC_StartTransition", RpcTarget.All);
        
        if (showDebugLogs)
            Debug.Log($"[Checkpoint] Transitioning in {transitionDelay} seconds...");
        
        // Wait for delay
        yield return new WaitForSeconds(transitionDelay);
        
        // Play transition sound
        if (audioSource != null && transitionSound != null)
            audioSource.PlayOneShot(transitionSound);
        
        // Small delay for sound
        yield return new WaitForSeconds(0.5f);
        
        // Clear inventories for all players
        photonView.RPC("RPC_ClearInventory", RpcTarget.All);
        
        // Small delay before loading
        yield return new WaitForSeconds(0.2f);
        
        // Load next level (PhotonNetwork.LoadLevel syncs for all players)
        if (showDebugLogs)
            Debug.Log($"[Checkpoint] Loading {nextLevelScene}...");
        
        PhotonNetwork.LoadLevel(nextLevelScene);
    }
    
    [PunRPC]
    void RPC_StartTransition()
    {
        isTransitioning = true;
        
        if (showDebugLogs)
            Debug.Log("[Checkpoint] RPC - Transition starting!");
        
        // Update UI
        if (statusText != null)
            statusText.text = "All players ready!\nLoading next level...";
        
        // Show all ready visual
        if (allReadyVisual != null)
            allReadyVisual.SetActive(true);
    }
    
    [PunRPC]
    void RPC_ClearInventory()
    {
        if (showDebugLogs)
            Debug.Log("[Checkpoint] RPC - Clearing inventory...");
        
        // Find local player's inventory and clear it
        InventorySystem[] inventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        
        foreach (var inventory in inventories)
        {
            PhotonView pv = inventory.GetComponent<PhotonView>();
            
            // Only clear local player's inventory
            if (pv != null && pv.IsMine)
            {
                inventory.ClearInventory();
                Debug.Log("[Checkpoint] ✅ Local player inventory cleared!");
                break;
            }
        }
    }
    
    // Handle player disconnect
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (showDebugLogs)
            Debug.Log($"[Checkpoint] Player {otherPlayer.NickName} left the room");
        
        // Recount players
        UpdatePlayerCount();
        UpdateUI();
    }
    
    // Visualize checkpoint area in Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            
            // Wire frame
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
            $"Checkpoint\nNext: {nextLevelScene}\nRequired: {requiredPlayers} players");
        #endif
    }
}