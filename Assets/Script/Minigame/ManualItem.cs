using UnityEngine;
using Photon.Pun;

/// <summary>
/// ManualItem.cs - SIMPLIFIED VERSION
/// 
/// SETUP:
/// 1. Put this ONLY on the parent object (Manual_Pickup_Parent)
/// 2. Do NOT put on child model (Book_Model)
/// 3. The Book_Model should just have Mesh Renderer, NO scripts
/// 
/// HIERARCHY:
/// Manual_Pickup_Parent     ← ManualItem.cs, PhotonView, Box Collider (Is Trigger ✓)
///   └── Book_Model         ← Just the 3D mesh, NO ManualItem!
///   └── Prompt_Canvas      ← World Space canvas for "Press E" prompt
/// </summary>
public class ManualItem : Item
{
    [Header("=== MANUAL SETTINGS ===")]
    public KeyCode openManualKey = KeyCode.Tab;
    public bool canOpenAnytime = true;
    
    [Header("References")]
    [Tooltip("The 3D model child to hide when collected")]
    public GameObject modelToHide;
    
    [Header("Prompt UI")]
    [Tooltip("The prompt canvas to show/hide")]
    public GameObject promptCanvas;
    
    // Static tracking - synced across network
    private static bool manualCollected = false;
    public static int ManualHolderActorNumber = -1;
    
    // Reference to UI
    private static ManualUI manualUIInstance;
    
    void Start()
    {
        // Configure item properties
        isConsumable = false;
        isThrowable = false;
        makesSound = false;
        
        if (string.IsNullOrEmpty(itemName))
        {
            itemName = "Manual CCTV";
        }
        
        // Hide prompt at start
        if (promptCanvas != null)
        {
            promptCanvas.SetActive(false);
        }
        
        // Reset state on scene load
        ResetState();
        
        Debug.Log("<color=cyan>[ManualItem] Initialized and ready for pickup</color>");
    }
    
    /// <summary>
    /// Override Use() - Called when player uses item from inventory
    /// </summary>
    public override void Use()
    {
        Debug.Log($"<color=cyan>[ManualItem] Use() called - Opening manual UI</color>");
        OpenManualUI();
    }
    
    /// <summary>
    /// Open the manual UI
    /// </summary>
    public static void OpenManualUI()
    {
        if (manualUIInstance == null)
        {
            manualUIInstance = Object.FindObjectOfType<ManualUI>(true);
        }
        
        if (manualUIInstance != null)
        {
            manualUIInstance.ToggleManual();
        }
        else
        {
            Debug.LogError("[ManualItem] ManualUI not found in scene!");
        }
    }
    
    /// <summary>
    /// Called when manual is picked up
    /// </summary>
    public void OnPickedUp(int actorNumber)
    {
        Debug.Log($"<color=lime>[ManualItem] OnPickedUp called by actor {actorNumber}</color>");
        
        // Hide the model
        if (modelToHide != null)
        {
            modelToHide.SetActive(false);
        }
        
        // Hide prompt
        if (promptCanvas != null)
        {
            promptCanvas.SetActive(false);
        }
    }
    
    // ==================== STATIC HELPERS ====================
    
    public static bool IsManualCollected()
    {
        return manualCollected;
    }
    
    public static bool LocalPlayerHasManual()
    {
        if (!PhotonNetwork.IsConnected) 
        {
            return manualCollected;
        }
        
        return ManualHolderActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
    }
    
    /// <summary>
    /// Set manual as collected - called from InventorySystem RPC
    /// </summary>
    public static void SetCollected(int collectorActorNumber)
    {
        manualCollected = true;
        ManualHolderActorNumber = collectorActorNumber;
        Debug.Log($"<color=lime>[ManualItem] SetCollected - Holder: {collectorActorNumber}</color>");
    }
    
    /// <summary>
    /// Reset static state
    /// </summary>
    public static void ResetState()
    {
        manualCollected = false;
        ManualHolderActorNumber = -1;
        manualUIInstance = null;
    }
}