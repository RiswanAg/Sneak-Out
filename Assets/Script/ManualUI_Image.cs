using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// ManualUI_Image.cs - Image-based manual system
/// Replaces TextMeshPro with manual page images
/// 
/// SETUP:
/// 1. Create manual page images (Page1_Wires.png, Page2_Button.png, Page3_Memory.png)
/// 2. Import to Unity
/// 3. Assign to ManualUI_Image component
/// 4. Pages will switch automatically when modules change
/// </summary>
public class ManualUI_Image : MonoBehaviour
{
    [Header("Manual Pages")]
    [Tooltip("Manual page for Wires module")]
    public Sprite page1_WiresImage;
    
    [Tooltip("Manual page for Button/Keypad module")]
    public Sprite page2_ButtonImage;
    
    [Tooltip("Manual page for Memory module")]
    public Sprite page3_MemoryImage;
    
    [Header("UI References")]
    [Tooltip("The Image component that displays manual pages")]
    public Image manualPageDisplay;
    
    [Tooltip("Parent panel/container")]
    public GameObject manualPanel;
    
    [Header("Navigation Buttons (Optional)")]
    [Tooltip("Button to go to previous page")]
    public Button previousButton;
    
    [Tooltip("Button to go to next page")]
    public Button nextButton;
    
    [Tooltip("Button to close manual")]
    public Button closeButton;
    
    [Header("Page Indicator (Optional)")]
    [Tooltip("Text showing current page number")]
    public TMPro.TMP_Text pageNumberText;
    
    [Header("Settings")]
    [Tooltip("Toggle manual with this key")]
    public KeyCode toggleKey = KeyCode.M;
    
    [Tooltip("Can toggle manual? (controlled by ControlPanelManager)")]
    private bool canToggle = false;
    
    // State
    private int currentPage = 0;
    private Sprite[] manualPages;
    private bool isManualOpen = false;
    
    void Start()
    {
        // Setup manual pages array
        manualPages = new Sprite[]
        {
            page1_WiresImage,
            page2_ButtonImage,
            page3_MemoryImage
        };
        
        // Setup buttons
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);
        
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
        
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseManual);
        
        // Hide manual initially
        if (manualPanel != null)
            manualPanel.SetActive(false);
        
        isManualOpen = false;
        
        // Show first page
        ShowPage(0);
    }
    
    void Update()
    {
        // Toggle manual with key
        if (Input.GetKeyDown(toggleKey) && canToggle)
        {
            ToggleManual();
        }
    }
    
    // ==================== MANUAL CONTROL ====================
    
    public void ToggleManual()
    {
        if (!canToggle) return;
        
        isManualOpen = !isManualOpen;
        
        if (manualPanel != null)
            manualPanel.SetActive(isManualOpen);
        
        GameLog.Log($"[Manual] Manual {(isManualOpen ? "OPENED" : "CLOSED")}");
    }
    
    public void OpenManual()
    {
        if (!canToggle) return;
        
        isManualOpen = true;
        
        if (manualPanel != null)
            manualPanel.SetActive(true);
        
        GameLog.Log("[Manual] Manual opened");
    }
    
    public void CloseManual()
    {
        isManualOpen = false;
        
        if (manualPanel != null)
            manualPanel.SetActive(false);
        
        GameLog.Log("[Manual] Manual closed");
    }
    
    // ==================== PAGE NAVIGATION ====================
    
    public void ShowPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= manualPages.Length)
        {
            Debug.LogWarning($"[Manual] Invalid page index: {pageIndex}");
            return;
        }
        
        currentPage = pageIndex;
        
        // Update display image
        if (manualPageDisplay != null && manualPages[currentPage] != null)
        {
            manualPageDisplay.sprite = manualPages[currentPage];
            GameLog.Log($"[Manual] Showing page {currentPage + 1}: {GetPageName(currentPage)}");
        }
        else
        {
            Debug.LogWarning($"[Manual] Page {currentPage + 1} image not assigned!");
        }
        
        // Update page number text
        UpdatePageNumber();
        
        // Update navigation buttons
        UpdateNavigationButtons();
    }
    
    public void NextPage()
    {
        if (currentPage < manualPages.Length - 1)
        {
            ShowPage(currentPage + 1);
        }
    }
    
    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            ShowPage(currentPage - 1);
        }
    }
    
    void UpdatePageNumber()
    {
        if (pageNumberText != null)
        {
            pageNumberText.text = $"Page {currentPage + 1}/{manualPages.Length}";
        }
    }
    
    void UpdateNavigationButtons()
    {
        // Disable previous button on first page
        if (previousButton != null)
        {
            previousButton.interactable = (currentPage > 0);
        }
        
        // Disable next button on last page
        if (nextButton != null)
        {
            nextButton.interactable = (currentPage < manualPages.Length - 1);
        }
    }
    
    string GetPageName(int pageIndex)
    {
        string[] pageNames = { "Wires", "Button", "Memory" };
        if (pageIndex < pageNames.Length)
            return pageNames[pageIndex];
        return "Unknown";
    }
    
    // ==================== CALLED BY CONTROL PANEL MANAGER ====================
    
    /// <summary>
    /// Enable/disable manual toggle (called by ControlPanelManager)
    /// </summary>
    public void SetCanToggle(bool canToggle)
    {
        this.canToggle = canToggle;
        GameLog.Log($"[Manual] Can toggle: {canToggle}");
        
        // Auto-close if disabled
        if (!canToggle && isManualOpen)
        {
            CloseManual();
        }
    }
    
    /// <summary>
    /// Check if local player has manual
    /// </summary>
    public bool HasManual()
    {
        // Check if manual item was collected
        return ManualItem.LocalPlayerHasManual();
    }
    
    /// <summary>
    /// Switch to specific module page (called by ControlPanelManager)
    /// </summary>
    public void SwitchToModule(int moduleIndex)
    {
        ShowPage(moduleIndex);
    }
    
    void OnDestroy()
    {
        // Clean up button listeners
        if (previousButton != null)
            previousButton.onClick.RemoveListener(PreviousPage);
        
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextPage);
        
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseManual);
    }
}
