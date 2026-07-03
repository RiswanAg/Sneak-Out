using UnityEngine;
using Photon.Pun;

/// <summary>
/// Player-side vision detection component
/// Each player checks if CikguNPC can see THEM, then notifies CikguNPC via RPC
/// This allows ALL players (Master and Client) to be detected by CikguNPC
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PlayerVisionDetector : MonoBehaviourPun
{
    [Header("Detection Settings")]
    [Tooltip("How often to check if CikguNPC can see this player (seconds)")]
    public float checkInterval = 0.2f;
    
    [Tooltip("Layer mask for vision blocking (walls, obstacles)")]
    public LayerMask visionBlockingLayers;
    
    private float checkTimer;
    private CikguNPC[] cikgus;
    private bool wasSpottedLastFrame = false;
    
    void Start()
    {
        // Only run on local player
        if (!photonView.IsMine) 
        {
            enabled = false;
            return;
        }
        
        // Find all CikguNPC instances in scene
        cikgus = FindObjectsOfType<CikguNPC>();
        
        // Set vision blocking layers if not set
        if (visionBlockingLayers.value == 0)
        {
            visionBlockingLayers = LayerMask.GetMask("Default", "Wall", "Obstacle");
        }
        
        GameLog.Log($"[PlayerVisionDetector] {gameObject.name} initialized - Found {cikgus.Length} CikguNPC(s)");
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        checkTimer -= Time.deltaTime;
        
        if (checkTimer <= 0f)
        {
            checkTimer = checkInterval;
            CheckIfCikguCanSeeMe();
        }
    }
    
    /// <summary>
    /// Check if ANY CikguNPC can see this player
    /// </summary>
    void CheckIfCikguCanSeeMe()
    {
        bool spottedThisFrame = false;
        
        foreach (CikguNPC cikgu in cikgus)
        {
            if (cikgu == null) continue;
            
            // Check if this CikguNPC can see me
            if (CanCikguSeeMe(cikgu))
            {
                spottedThisFrame = true;
                
                // Only notify once (when first spotted)
                if (!wasSpottedLastFrame)
                {
                    NotifyCikguSpotted(cikgu);
                }
                
                break; // One spotting is enough
            }
        }
        
        wasSpottedLastFrame = spottedThisFrame;
    }
    
    /// <summary>
    /// Check if a specific CikguNPC can see this player
    /// </summary>
    bool CanCikguSeeMe(CikguNPC cikgu)
    {
        // Check if CikguNPC has vision enabled
        if (!cikgu.hasVision) return false;
        
        // Don't check if CikguNPC is in Yelling state (game over)
        // We'll access this via a public property or method
        
        Vector3 cikguEyePos = cikgu.transform.position + Vector3.up * 1.6f;
        Vector3 myPosition = transform.position + Vector3.up * 1f;
        
        // Check distance
        float distance = Vector3.Distance(cikguEyePos, myPosition);
        if (distance > cikgu.visionRange) return false;
        
        // Check angle
        Vector3 dirToMe = (myPosition - cikguEyePos).normalized;
        float angle = Vector3.Angle(cikgu.transform.forward, dirToMe);
        if (angle > cikgu.visionAngle / 2f) return false;
        
        // Check line of sight (raycast)
        RaycastHit hit;
        if (Physics.Raycast(cikguEyePos, dirToMe, out hit, distance, visionBlockingLayers))
        {
            // Something is blocking vision
            if (hit.collider.gameObject != gameObject) return false;
        }
        
        // CikguNPC can see me!
        return true;
    }
    
    /// <summary>
    /// Notify CikguNPC that it spotted this player (via RPC to Master)
    /// </summary>
    void NotifyCikguSpotted(CikguNPC cikgu)
    {
        GameLog.Log($"[PlayerVisionDetector] {gameObject.name} spotted by CikguNPC! Notifying...");
        
        // Get CikguNPC's PhotonView
        PhotonView cikguPV = cikgu.GetComponent<PhotonView>();
        if (cikguPV == null)
        {
            Debug.LogError("[PlayerVisionDetector] CikguNPC has no PhotonView!");
            return;
        }
        
        // Send RPC to CikguNPC to notify it was spotted
        // Pass our PhotonView ID so CikguNPC knows which player to chase
        cikguPV.RPC("RPC_PlayerSpotted", RpcTarget.All, photonView.ViewID);
    }
}
