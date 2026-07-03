using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// WireVisual.cs - Enhanced visual component for individual wires
/// Attach to each wire button for better visual feedback
/// Shows cut animation and hover effects
/// </summary>
public class WireVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual References")]
    public Image wireImage;                // The wire line itself
    public Image wireLeftCap;              // Left end cap (optional)
    public Image wireRightCap;             // Right end cap (optional)
    public GameObject cutEffect;           // Particle/sprite when cut
    public GameObject hoverHighlight;      // Highlight when hovering
    
    [Header("Cut Animation")]
    public float cutAnimationDuration = 0.3f;
    public AnimationCurve cutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Colors")]
    public Color hoverTint = new Color(1.2f, 1.2f, 1.2f, 1f);
    
    private Color originalColor;
    private bool isCut = false;
    private bool isHovered = false;
    
    void Start()
    {
        if (wireImage != null)
            originalColor = wireImage.color;
        
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
        
        if (cutEffect != null)
            cutEffect.SetActive(false);
    }
    
    /// <summary>
    /// Set the wire's color
    /// </summary>
    public void SetWireColor(Color color)
    {
        originalColor = color;
        
        if (wireImage != null)
            wireImage.color = color;
        
        // Slightly darker caps for depth
        if (wireLeftCap != null)
            wireLeftCap.color = color * 0.8f;
        if (wireRightCap != null)
            wireRightCap.color = color * 0.8f;
    }
    
    /// <summary>
    /// Play cut animation
    /// </summary>
    public void Cut()
    {
        if (isCut) return;
        isCut = true;
        
        StartCoroutine(CutAnimation());
    }
    
    IEnumerator CutAnimation()
    {
        // Show cut effect
        if (cutEffect != null)
        {
            cutEffect.SetActive(true);
            cutEffect.transform.position = wireImage.transform.position;
        }
        
        // Animate wire separating
        float elapsed = 0f;
        Vector3 originalScale = wireImage.transform.localScale;
        
        while (elapsed < cutAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = cutCurve.Evaluate(elapsed / cutAnimationDuration);
            
            // Shrink wire horizontally
            wireImage.transform.localScale = new Vector3(
                Mathf.Lerp(originalScale.x, 0f, t),
                originalScale.y,
                originalScale.z
            );
            
            // Fade color
            Color fadedColor = originalColor;
            fadedColor.a = Mathf.Lerp(1f, 0.3f, t);
            wireImage.color = fadedColor;
            
            yield return null;
        }
        
        // Final state - gray/cut appearance
        wireImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        
        // Hide highlight
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isCut) return;
        
        isHovered = true;
        
        // Show highlight
        if (hoverHighlight != null)
            hoverHighlight.SetActive(true);
        
        // Tint wire
        if (wireImage != null)
            wireImage.color = originalColor * hoverTint;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isCut) return;
        
        isHovered = false;
        
        // Hide highlight
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
        
        // Reset color
        if (wireImage != null)
            wireImage.color = originalColor;
    }
    
    /// <summary>
    /// Reset wire to initial state (for retry)
    /// </summary>
    public void ResetWire()
    {
        isCut = false;
        
        if (wireImage != null)
        {
            wireImage.transform.localScale = Vector3.one;
            wireImage.color = originalColor;
        }
        
        if (cutEffect != null)
            cutEffect.SetActive(false);
        
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
    }
}
