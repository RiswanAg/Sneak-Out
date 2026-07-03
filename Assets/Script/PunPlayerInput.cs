using UnityEngine;
using Photon.Pun;

public class PunPlayerInput : MonoBehaviourPun
{
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    void Start()
    {
        // Only disable if this is NOT our player
        if (!photonView.IsMine)
        {
            DisableRemotePlayerInput();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"<color=green>✅ Local player spawned: {gameObject.name}</color>");
        }
    }
    
    void DisableRemotePlayerInput()
    {
        if (showDebugLogs)
            Debug.Log($"<color=yellow>Disabling input for remote player: {gameObject.name}</color>");
        
        // Disable ALL MonoBehaviour scripts that handle input
        // Get all components
        MonoBehaviour[] allComponents = GetComponents<MonoBehaviour>();
        
        foreach (MonoBehaviour component in allComponents)
        {
            if (component == null) continue;
            
            // Don't disable Photon components or this script
            if (component is MonoBehaviourPun) continue;
            if (component == this) continue;
            
            // Get component type name
            string typeName = component.GetType().Name;
            
            // Disable input-related components
            if (typeName.Contains("Controller") || 
                typeName.Contains("Input") || 
                typeName.Contains("Pickup") ||
                typeName.Contains("Inventory") ||
                typeName.Contains("Throw") ||
                typeName.Contains("Sound"))
            {
                component.enabled = false;
                
                if (showDebugLogs)
                    Debug.Log($"  - Disabled: {typeName}");
            }
        }
        
        // Also disable Animator to save performance
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            // Don't disable animator, just reduce update rate
            // animator.enabled = false; // Commented out - causes issues
        }
        
        if (showDebugLogs)
            Debug.Log($"<color=yellow>✅ Remote player input disabled</color>");
    }
}