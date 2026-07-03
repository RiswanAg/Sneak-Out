using UnityEngine;
using Photon.Pun;

public class InteractableFurniture : MonoBehaviourPun
{
    [Header("Door Settings")]
    public Transform door;   
    public float openAngle = 90f; 
    public float openSpeed = 4f;
    
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool playerIsNear = false;

    void Start()
    {
        if (door == null)
        {
            Debug.LogError($"InteractableFurniture on {gameObject.name}: Door Transform not assigned!");
            enabled = false;
            return;
        }
        
        closedRotation = door.localRotation;
        openRotation = Quaternion.Euler(door.localEulerAngles + new Vector3(0, openAngle, 0));
        
        if (showDebugLogs)
        {
            GameLog.Log($"InteractableFurniture: {gameObject.name} initialized");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(interactKey) && playerIsNear)
        {
            if (showDebugLogs) GameLog.Log($"Player pressed {interactKey} near {gameObject.name}");
            
            // Check if we're connected to Photon
            if (PhotonNetwork.IsConnected && photonView != null)
            {
                // Send RPC to all clients to toggle the door
                photonView.RPC("ToggleDoor", RpcTarget.AllBuffered);
            }
            else
            {
                // Single player / offline mode - just toggle locally
                ToggleDoor();
            }
        }

        // Smoothly interpolate door rotation
        if (door != null)
        {
            if (isOpen)
                door.localRotation = Quaternion.Slerp(door.localRotation, openRotation, Time.deltaTime * openSpeed);
            else
                door.localRotation = Quaternion.Slerp(door.localRotation, closedRotation, Time.deltaTime * openSpeed);
        }
    }

    [PunRPC]
    void ToggleDoor()
    {
        isOpen = !isOpen;
        if (showDebugLogs) GameLog.Log($"🚪 {gameObject.name} door: {(isOpen ? "OPEN" : "CLOSED")}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ✅ FIXED: Check PhotonView more safely
            PhotonView pv = other.GetComponent<PhotonView>();
            
            // If no PhotonView (offline/single player) OR it's the local player
            if (pv == null || pv.IsMine)
            {
                playerIsNear = true;
                if (showDebugLogs) GameLog.Log($"✅ Player near {gameObject.name}. Press {interactKey} to interact.");
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
                if (showDebugLogs) GameLog.Log($"Player left {gameObject.name} trigger area");
            }
        }
    }
    
    // Visualize trigger in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}