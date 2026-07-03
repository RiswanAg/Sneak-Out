using UnityEngine;

/// <summary>
/// GameOverScreenDebugger.cs - Attach to GameOverScreen to debug visibility issues
/// </summary>
public class GameOverScreenDebugger : MonoBehaviour
{
    void OnEnable()
    {
        Debug.Log($"<color=green>✅ GameOverScreen ENABLED!</color>");
        Debug.Log($"   - GameObject name: {gameObject.name}");
        Debug.Log($"   - Is active: {gameObject.activeInHierarchy}");
        Debug.Log($"   - Position: {transform.position}");
        
        // Check Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"   - Canvas found: {canvas.name}");
            Debug.Log($"   - Canvas enabled: {canvas.enabled}");
            Debug.Log($"   - Canvas sort order: {canvas.sortingOrder}");
            Debug.Log($"   - Render mode: {canvas.renderMode}");
        }
        else
        {
            Debug.LogError("   - ❌ NO CANVAS FOUND! GameOverScreen needs a Canvas parent!");
        }
        
        // Check CanvasGroup (might be blocking)
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            Debug.Log($"   - CanvasGroup alpha: {canvasGroup.alpha}");
            Debug.Log($"   - CanvasGroup interactable: {canvasGroup.interactable}");
            Debug.Log($"   - CanvasGroup blocksRaycasts: {canvasGroup.blocksRaycasts}");
            
            if (canvasGroup.alpha == 0)
            {
                Debug.LogWarning("   - ⚠️ CanvasGroup alpha is 0! Setting to 1...");
                canvasGroup.alpha = 1f;
            }
        }
        
        // Check if there are any Image/Text components
        var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
        var texts = GetComponentsInChildren<TMPro.TMP_Text>(true);
        
        Debug.Log($"   - Found {images.Length} Image components");
        Debug.Log($"   - Found {texts.Length} Text components");
        
        // List all children
        Debug.Log($"   - Children:");
        foreach (Transform child in transform)
        {
            Debug.Log($"      • {child.name} (active: {child.gameObject.activeSelf})");
        }
    }
    
    void OnDisable()
    {
        Debug.Log($"<color=red>❌ GameOverScreen DISABLED!</color>");
    }
}