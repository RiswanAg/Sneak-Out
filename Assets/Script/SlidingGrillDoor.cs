using UnityEngine;
using Photon.Pun;

public class SlidingGrillDoor : MonoBehaviourPun
{
    [Header("Door References")]
    [Tooltip("Drag the RIGHT door Transform here (the one that slides)")]
    public Transform rightDoor;
    
    [Header("Sliding Animation")]
    [Tooltip("How far the door slides open (in meters)")]
    public float slideDistance = 2f;
    
    [Tooltip("Speed of sliding animation")]
    public float slideSpeed = 2f;
    
    [Tooltip("Direction to slide: X, Y, or Z axis")]
    public SlideDirection slideDirection = SlideDirection.X_Right;
    
    public enum SlideDirection
    {
        X_Right,    // Slide along positive X
        X_Left,     // Slide along negative X
        Z_Forward,  // Slide along positive Z
        Z_Back      // Slide along negative Z
    }
    
    [Header("Interaction")]
    [Tooltip("Key to interact with the door")]
    public KeyCode interactKey = KeyCode.E;
    
    [Header("Key Requirement")]
    [Tooltip("Name of the key item required (must match Item.itemName)")]
    public string requiredKeyName = "Grill Key";
    
    [Header("UI Feedback (Optional)")]
    [Tooltip("UI message shown when player doesn't have key")]
    public GameObject lockedMessageUI;
    
    [Tooltip("How long to display the locked message")]
    public float messageDisplayTime = 3f;
    
    [Header("Audio (Optional)")]
    public AudioClip openSound;
    public AudioClip lockedSound;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private AudioSource audioSource;
    private bool isOpen = false;
    private bool isUnlocked = false;
    private bool playerIsNear = false;
    private Vector3 closedPosition;
    private Vector3 openPosition;
    
    void Start()
    {
        if (rightDoor == null)
        {
            Debug.LogError("SlidingGrillDoor: Right Door must be assigned!");
            enabled = false;
            return;
        }
        
        // Save initial position
        closedPosition = rightDoor.localPosition;
        
        // Calculate open position based on slide direction
        Vector3 slideOffset = Vector3.zero;
        switch (slideDirection)
        {
            case SlideDirection.X_Right:
                slideOffset = new Vector3(slideDistance, 0, 0);
                break;
            case SlideDirection.X_Left:
                slideOffset = new Vector3(-slideDistance, 0, 0);
                break;
            case SlideDirection.Z_Forward:
                slideOffset = new Vector3(0, 0, slideDistance);
                break;
            case SlideDirection.Z_Back:
                slideOffset = new Vector3(0, 0, -slideDistance);
                break;
        }
        openPosition = closedPosition + slideOffset;
        
        if (showDebugLogs)
        {
            Debug.Log($"SlidingGrillDoor: Closed pos = {closedPosition}, Open pos = {openPosition}");
        }
        
        // Setup UI
        if (lockedMessageUI != null)
            lockedMessageUI.SetActive(false);
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (openSound != null || lockedSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }
    
    void Update()
    {
        // Check for interaction
        if (playerIsNear && Input.GetKeyDown(interactKey))
        {
            if (showDebugLogs) Debug.Log("Player pressed E near grill door");
            TryInteract();
        }
        
        // Smoothly animate door
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;
        rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, targetPosition, Time.deltaTime * slideSpeed);
    }
    
    void TryInteract()
    {
        // If already unlocked, just toggle open/close
        if (isUnlocked)
        {
            ToggleDoor();
            return;
        }
        
        // Find player's inventory
        InventorySystem inventory = FindPlayerInventory();
        
        if (inventory == null)
        {
            Debug.LogError("SlidingGrillDoor: Could not find InventorySystem!");
            return;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Found inventory on: {inventory.gameObject.name}");
            Debug.Log($"Checking for item: '{requiredKeyName}'");
            Debug.Log($"HasItem result: {inventory.HasItem(requiredKeyName)}");
        }
        
        // Check if player has the key
        if (inventory.HasItem(requiredKeyName))
        {
            if (showDebugLogs) Debug.Log($"✅ Player has {requiredKeyName}! Opening door...");
            
            isUnlocked = true;
            
            // Sync across network
            if (photonView != null && PhotonNetwork.IsConnected)
            {
                photonView.RPC("RPC_OpenDoor", RpcTarget.AllBuffered);
            }
            else
            {
                isOpen = true;
            }
            
            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
            
            // ✅ ALWAYS consume the key after unlocking
            inventory.RemoveItemByName(requiredKeyName);
            if (showDebugLogs) Debug.Log($"🔑 Key '{requiredKeyName}' consumed. Door permanently unlocked!");
        }
        else
        {
            if (showDebugLogs) Debug.Log($"❌ Need {requiredKeyName} to open!");
            
            if (audioSource != null && lockedSound != null)
            {
                audioSource.PlayOneShot(lockedSound);
            }
            
            ShowLockedMessage();
        }
    }
    
    void ToggleDoor()
    {
        if (photonView != null && PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_ToggleDoor", RpcTarget.AllBuffered);
        }
        else
        {
            isOpen = !isOpen;
        }
        
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }
        
        if (showDebugLogs) Debug.Log($"Door: {(isOpen ? "OPEN" : "CLOSED")}");
    }
    
    InventorySystem FindPlayerInventory()
    {
        // Method 1: Find by PhotonView.IsMine
        InventorySystem[] allInventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        foreach (var inv in allInventories)
        {
            PhotonView pv = inv.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                return inv;
            }
        }
        
        // Method 2: Find by Player tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            InventorySystem inv = player.GetComponent<InventorySystem>();
            if (inv != null) return inv;
            
            inv = player.GetComponentInChildren<InventorySystem>();
            if (inv != null) return inv;
        }
        
        // Method 3: Fallback - find any
        return FindFirstObjectByType<InventorySystem>();
    }
    
    [PunRPC]
    void RPC_OpenDoor()
    {
        isOpen = true;
        isUnlocked = true;
        if (showDebugLogs) Debug.Log("RPC: Door opened");
    }
    
    [PunRPC]
    void RPC_ToggleDoor()
    {
        isOpen = !isOpen;
        if (showDebugLogs) Debug.Log($"RPC: Door {(isOpen ? "OPEN" : "CLOSED")}");
    }
    
    void ShowLockedMessage()
    {
        if (lockedMessageUI != null)
        {
            StopAllCoroutines();
            StartCoroutine(DisplayLockedMessage());
        }
    }
    
    System.Collections.IEnumerator DisplayLockedMessage()
    {
        lockedMessageUI.SetActive(true);
        yield return new WaitForSeconds(messageDisplayTime);
        lockedMessageUI.SetActive(false);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv == null || pv.IsMine)
            {
                playerIsNear = true;
                if (showDebugLogs) Debug.Log($"✅ Player near door. Press {interactKey} to interact.");
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv == null || pv.IsMine)
            {
                playerIsNear = false;
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        
        if (rightDoor != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 start = rightDoor.position;
            Vector3 end = start;
            
            switch (slideDirection)
            {
                case SlideDirection.X_Right:
                    end += rightDoor.right * slideDistance;
                    break;
                case SlideDirection.X_Left:
                    end -= rightDoor.right * slideDistance;
                    break;
                case SlideDirection.Z_Forward:
                    end += rightDoor.forward * slideDistance;
                    break;
                case SlideDirection.Z_Back:
                    end -= rightDoor.forward * slideDistance;
                    break;
            }
            
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.1f);
        }
    }
}