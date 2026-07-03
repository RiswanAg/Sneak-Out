using UnityEngine;
using Photon.Pun;
using TMPro;

/// <summary>
/// Attach to a trigger zone for Level 2 checkpoint/exit
/// When ALL players are inside, they win!
/// </summary>
[RequireComponent(typeof(Collider))]
public class Level2Checkpoint : MonoBehaviour
{
    [Header("UI Feedback")]
    [Tooltip("Optional: Text to show waiting status")]
    public TMP_Text waitingText;
    
    [Tooltip("Optional: Panel to show when player is in checkpoint")]
    public GameObject waitingPanel;
    
    [Header("Settings")]
    public int requiredPlayers = 2;
    
    private bool localPlayerInside = false;
    
    void Start()
    {
        // Ensure collider is trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        
        // Hide UI
        if (waitingPanel != null)
            waitingPanel.SetActive(false);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine) return;
        
        // Local player entered checkpoint
        localPlayerInside = true;
        
        GameLog.Log($"[Checkpoint] Local player entered checkpoint");
        
        // Notify Level2Manager
        if (Level2Manager.Instance != null)
        {
            Level2Manager.Instance.PlayerEnteredCheckpoint(PhotonNetwork.LocalPlayer.ActorNumber);
        }
        
        // Show waiting UI
        UpdateUI();
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv == null || !pv.IsMine) return;
        
        // Local player exited checkpoint
        localPlayerInside = false;
        
        GameLog.Log($"[Checkpoint] Local player exited checkpoint");
        
        // Notify Level2Manager
        if (Level2Manager.Instance != null)
        {
            Level2Manager.Instance.PlayerExitedCheckpoint(PhotonNetwork.LocalPlayer.ActorNumber);
        }
        
        // Hide waiting UI
        if (waitingPanel != null)
            waitingPanel.SetActive(false);
    }
    
    void UpdateUI()
    {
        if (waitingPanel != null)
            waitingPanel.SetActive(true);
        
        if (waitingText != null)
        {
            waitingText.text = "Menunggu pemain lain...";
        }
    }
    
    void OnDrawGizmos()
    {
        // Visualize checkpoint area
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
}