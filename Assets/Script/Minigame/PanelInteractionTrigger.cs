using UnityEngine;
using Photon.Pun;
using TMPro;

/// <summary>
/// PanelInteractionTrigger.cs - Handles player interaction with the control panel
/// 
/// RULE: Only player who DOES NOT have the manual can operate the panel!
/// - Player with manual = Coordinator (reads manual, guides)
/// - Player without manual = Operator (uses panel)
/// </summary>
public class PanelInteractionTrigger : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    
    [Header("UI Prompt")]
    public GameObject interactPrompt;
    public TMP_Text promptText;
    
    [Header("References")]
    public ControlPanelManager panelManager;
    
    [Header("Requirements")]
    [Tooltip("Manual must be collected by SOMEONE (not necessarily this player)")]
    public bool requireManualCollected = true;
    
    // State
    private bool playerInRange = false;
    
    void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
        
        if (panelManager == null)
            panelManager = FindObjectOfType<ControlPanelManager>();
    }
    
    void Update()
    {
        if (!playerInRange) return;
        
        // Check if puzzle is already active
        if (panelManager != null && panelManager.IsPuzzleActive())
        {
            UpdatePromptText("Panel sedang digunakan...");
            return;
        }
        
        // Check if manual has been collected by ANYONE
        if (requireManualCollected && !ManualItem.IsManualCollected())
        {
            UpdatePromptText("Rakan perlu cari manual dahulu!");
            return;
        }
        
        // ✅ KEY CHECK: This player must NOT have the manual!
        if (ManualItem.LocalPlayerHasManual())
        {
            UpdatePromptText("Anda ada manual! Biar rakan operasi panel.");
            return;
        }
        
        // Player can operate the panel
        UpdatePromptText("Tekan E untuk akses Panel Kawalan");
        
        if (Input.GetKeyDown(interactKey))
        {
            TryStartPuzzle();
        }
    }
    
    void TryStartPuzzle()
    {
        if (panelManager == null)
        {
            Debug.LogError("ControlPanelManager not found!");
            return;
        }
        
        // Double-check: Manual must be collected by someone
        if (requireManualCollected && !ManualItem.IsManualCollected())
        {
            Debug.Log("Cannot start - manual not collected by anyone!");
            return;
        }
        
        // ✅ Double-check: This player must NOT have manual
        if (ManualItem.LocalPlayerHasManual())
        {
            Debug.Log("Cannot start - you have the manual! Let your teammate operate.");
            return;
        }
        
        if (panelManager.IsPuzzleActive())
        {
            Debug.Log("Panel already in use!");
            return;
        }
        
        // Start puzzle with this player as operator
        Debug.Log("<color=green>[Panel] Starting puzzle - you are the OPERATOR!</color>");
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        panelManager.StartPuzzle(actorNumber);
        
        // Hide prompt
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }
    
    void UpdatePromptText(string text)
    {
        if (promptText != null)
            promptText.text = text;
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Only respond to LOCAL player
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null) pv = other.GetComponentInParent<PhotonView>();
        
        if (pv != null && pv.IsMine)
        {
            playerInRange = true;
            
            if (interactPrompt != null)
                interactPrompt.SetActive(true);
                
            Debug.Log("[Panel] Player entered trigger range");
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null) pv = other.GetComponentInParent<PhotonView>();
        
        if (pv != null && pv.IsMine)
        {
            playerInRange = false;
            
            if (interactPrompt != null)
                interactPrompt.SetActive(false);
                
            Debug.Log("[Panel] Player left trigger range");
        }
    }
}