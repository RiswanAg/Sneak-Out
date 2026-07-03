using UnityEngine;

/// <summary>
/// PanelUIInputHandler.cs - Handles additional input while panel is open
/// Attach to PanelUI_Canvas
/// Handles ESC to cancel, and other keyboard shortcuts
/// </summary>
public class PanelUIInputHandler : MonoBehaviour
{
    [Header("References")]
    public ControlPanelManager panelManager;
    
    [Header("Keys")]
    public KeyCode cancelKey = KeyCode.Escape;
    
    void Start()
    {
        // Auto-find if not assigned
        if (panelManager == null)
            panelManager = FindObjectOfType<ControlPanelManager>();
    }
    
    void Update()
    {
        // Only handle input if we're the operator
        if (panelManager == null || !panelManager.IsLocalPlayerOperator()) return;
        
        // ESC to cancel puzzle
        if (Input.GetKeyDown(cancelKey))
        {
            Debug.Log("ESC pressed - canceling puzzle");
            panelManager.CancelPuzzle();
        }
    }
}
