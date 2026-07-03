using UnityEngine;
using Photon.Pun;

public class PunDisableCameraRootOnRemote : MonoBehaviourPun
{
    [Header("Camera Settings")]
    [Tooltip("GameObject yang contain Main Camera")]
    public GameObject playerCameraRoot;
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    void Awake()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CameraSetup] {gameObject.name} - IsMine: {photonView.IsMine}");
        }
        
        // If this is MY player
        if (photonView.IsMine)
        {
            // Enable camera
            if (playerCameraRoot != null)
            {
                playerCameraRoot.SetActive(true);
                Debug.Log($"<color=green>✅ Camera ENABLED for LOCAL player: {gameObject.name}</color>");
            }
            else
            {
                Debug.LogError($"❌ playerCameraRoot NOT ASSIGNED on {gameObject.name}!");
            }
        }
        else
        {
            // This is REMOTE player - disable camera
            if (playerCameraRoot != null)
            {
                playerCameraRoot.SetActive(false);
                Debug.Log($"<color=yellow>Camera DISABLED for REMOTE player: {gameObject.name}</color>");
            }
        }
    }
}