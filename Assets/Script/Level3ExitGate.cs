using UnityEngine;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Level3ExitGate.cs - School exit checkpoint for Level 3
/// Simplified version - No gate visuals, just checkpoint detection
/// 
/// BEHAVIOR:
/// - Both players must reach the checkpoint
/// - Plays success cutscene when both players arrive
/// - After cutscene, shows victory UI (Restart or Menu)
/// </summary>
public class Level3ExitGate : MonoBehaviourPun
{
    [Header("Player Tracking")]
    [Tooltip("Is Player 1 (Hazim) at checkpoint?")]
    private bool player1AtGate = false;
    
    [Tooltip("Is Player 2 (Amir) at checkpoint?")]
    private bool player2AtGate = false;
    
    [Header("UI")]
    [Tooltip("Prompt shown when waiting for other player")]
    public GameObject waitingPrompt;
    
    [Tooltip("Text component for waiting prompt")]
    public TMPro.TMP_Text waitingText;
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    [Tooltip("Sound when first player arrives")]
    public AudioClip arrivalSound;
    
    [Tooltip("Sound when both players arrive")]
    public AudioClip successSound;
    
    [Range(0f, 1f)]
    public float volume = 0.8f;
    
    // State
    private bool levelComplete = false;
    private int playersAtGate = 0;
    
    void Start()
    {
        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;
        
        // Hide waiting prompt initially
        if (waitingPrompt != null)
            waitingPrompt.SetActive(false);
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Only respond to local player
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine) return;
        
        // Check which player entered
        string character = PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("character") 
            ? PhotonNetwork.LocalPlayer.CustomProperties["character"].ToString() 
            : (PhotonNetwork.IsMasterClient ? "Hazim" : "Amir");
        
        Debug.Log($"<color=green>[Checkpoint] {character} reached the checkpoint!</color>");
        
        // Notify all clients that this player arrived
        photonView.RPC("RPC_PlayerArrived", RpcTarget.AllBuffered, character);
    }
    
    void OnTriggerExit(Collider other)
    {
        // Only respond to local player
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine) return;
        
        // Check which player left
        string character = PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("character") 
            ? PhotonNetwork.LocalPlayer.CustomProperties["character"].ToString() 
            : (PhotonNetwork.IsMasterClient ? "Hazim" : "Amir");
        
        Debug.Log($"<color=yellow>[Checkpoint] {character} left the checkpoint area</color>");
        
        // Notify all clients that this player left
        photonView.RPC("RPC_PlayerLeft", RpcTarget.AllBuffered, character);
    }
    
    [PunRPC]
    void RPC_PlayerArrived(string character)
    {
        if (levelComplete) return;
        
        // Mark player as arrived
        if (character == "Hazim")
        {
            if (!player1AtGate)
            {
                player1AtGate = true;
                playersAtGate++;
                Debug.Log($"[Checkpoint] Hazim arrived! ({playersAtGate}/2 players)");
            }
        }
        else if (character == "Amir")
        {
            if (!player2AtGate)
            {
                player2AtGate = true;
                playersAtGate++;
                Debug.Log($"[Checkpoint] Amir arrived! ({playersAtGate}/2 players)");
            }
        }
        
        // Check if both players are at checkpoint
        if (player1AtGate && player2AtGate)
        {
            Debug.Log("<color=green>[Checkpoint] ✅ BOTH PLAYERS ARRIVED - LEVEL COMPLETE!</color>");
            OnBothPlayersArrived();
        }
        else
        {
            // Show waiting prompt
            ShowWaitingPrompt();
            
            // Play arrival sound
            PlaySound(arrivalSound);
        }
    }
    
    [PunRPC]
    void RPC_PlayerLeft(string character)
    {
        if (levelComplete) return;
        
        // Mark player as left
        if (character == "Hazim")
        {
            if (player1AtGate)
            {
                player1AtGate = false;
                playersAtGate--;
                Debug.Log($"[Checkpoint] Hazim left! ({playersAtGate}/2 players)");
            }
        }
        else if (character == "Amir")
        {
            if (player2AtGate)
            {
                player2AtGate = false;
                playersAtGate--;
                Debug.Log($"[Checkpoint] Amir left! ({playersAtGate}/2 players)");
            }
        }
        
        // Hide waiting prompt if no one at checkpoint
        if (playersAtGate == 0)
        {
            HideWaitingPrompt();
        }
    }
    
    void OnBothPlayersArrived()
    {
        if (levelComplete) return;
        
        levelComplete = true;
        
        // Hide waiting prompt
        HideWaitingPrompt();
        
        // Play success sound
        PlaySound(successSound);
        
        // Trigger victory cutscene (only Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(TriggerVictoryCutscene());
        }
    }
    
    IEnumerator TriggerVictoryCutscene()
    {
        // Brief delay for dramatic effect
        yield return new WaitForSeconds(1f);
        
        Debug.Log("<color=cyan>[Checkpoint] Triggering victory cutscene!</color>");
        
        // Find and trigger the victory cutscene manager
        Level3VictoryCutsceneManager victoryManager = FindObjectOfType<Level3VictoryCutsceneManager>();
        
        if (victoryManager != null)
        {
            victoryManager.PlayVictoryCutscene();
        }
        else
        {
            Debug.LogError("[Checkpoint] Level3VictoryCutsceneManager not found! Showing victory UI directly...");
            
            // Fallback - show victory UI directly without cutscene
            Level3VictoryUI victoryUI = FindObjectOfType<Level3VictoryUI>();
            if (victoryUI != null)
            {
                victoryUI.ShowVictoryScreen();
            }
            else
            {
                Debug.LogError("[Checkpoint] Level3VictoryUI also not found!");
            }
        }
    }
    
    void ShowWaitingPrompt()
    {
        if (waitingPrompt != null)
        {
            waitingPrompt.SetActive(true);
            
            if (waitingText != null)
            {
                // Check which player is waiting
                bool localIsHazim = PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("character") 
                    && PhotonNetwork.LocalPlayer.CustomProperties["character"].ToString() == "Hazim";
                
                // Determine who we're waiting for
                string waitingFor = "";
                if (localIsHazim)
                {
                    waitingFor = player1AtGate ? "Amir" : "Hazim";
                }
                else
                {
                    waitingFor = player2AtGate ? "Hazim" : "Amir";
                }
                
                waitingText.text = $"Menunggu {waitingFor}...";
            }
        }
    }
    
    void HideWaitingPrompt()
    {
        if (waitingPrompt != null)
            waitingPrompt.SetActive(false);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw checkpoint trigger area
        Gizmos.color = Color.green;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}