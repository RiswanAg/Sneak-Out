using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

/// <summary>
/// ManualUI.cs - Image-based Manual Viewer (v2)
/// 
/// Uses IMAGE SPRITES instead of text for each page
/// Simple design: Shows one full-screen image at a time
/// Navigate with arrow buttons or arrow keys
/// </summary>
public class ManualUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject manualCanvas;
    public Image manualPageImage;           // Single Image component that changes sprite
    public Button nextPageButton;
    public Button prevPageButton;
    public Button closeButton;
    public TMP_Text pageNumberText;
    
    [Header("Manual Page Sprites")]
    public Sprite page1_Wires;              // WAYAR__1_.png
    public Sprite page2_Button;             // (your button module manual)
    public Sprite page3_Memory;             // MEMORI.png
    
    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.Tab;
    
    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip pageFlipSound;
    
    // State - STATIC so it persists
    private static bool hasManual = false;
    private bool isOpen = false;
    private int currentPage = 0;
    private int totalPages = 3;
    
    // Cached references for disabling
    private MonoBehaviour cachedPlayerController;
    
    // Singleton
    public static ManualUI Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        // Setup button listeners
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPage);
        if (prevPageButton != null)
            prevPageButton.onClick.AddListener(PrevPage);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseManual);
        
        // Start closed
        isOpen = false;
        if (manualCanvas != null)
            manualCanvas.SetActive(false);
    }
    
    void Update()
    {
        if (!hasManual) return;
        
        // TAB to toggle
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleManual();
        }
        
        // ESC to close
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseManual();
        }
        
        // Arrow keys for navigation when manual is open
        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
                NextPage();
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                PrevPage();
        }
    }
    
    /// <summary>
    /// LateUpdate ensures cursor stays visible even if other scripts try to hide it
    /// </summary>
    void LateUpdate()
    {
        if (isOpen)
        {
            // Force cursor to stay visible while manual is open
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
    
    public void OnManualCollected()
    {
        hasManual = true;
    }
    
    public void EnableManual()
    {
        OnManualCollected();
    }
    
    // ==================== TOGGLE MANUAL ====================
    
    public void ToggleManual()
    {
        if (!hasManual) return;
        
        if (isOpen)
            CloseManual();
        else
            OpenManual();
    }
    
    public void OpenManual()
    {
        if (!hasManual) return;
        if (isOpen) return;
        
        isOpen = true;
        
        // Show canvas
        if (manualCanvas != null)
        {
            manualCanvas.SetActive(true);
        }
        
        // Show current page
        ShowPage(currentPage);
        PlaySound(openSound);
        
        // Show cursor and unlock it
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        // Disable player controls
        DisablePlayerControls();
    }
    
    public void CloseManual()
    {
        if (!isOpen) return;
        
        isOpen = false;
        
        if (manualCanvas != null)
        {
            manualCanvas.SetActive(false);
        }
        
        PlaySound(closeSound);
        
        // Re-enable player controls FIRST
        EnablePlayerControls();
        
        // Then hide cursor (unless in puzzle)
        bool inPuzzle = ControlPanelManager.Instance != null && ControlPanelManager.Instance.IsPuzzleActive();
        
        if (!inPuzzle)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    // ==================== PAGE NAVIGATION ====================
    
    void ShowPage(int pageIndex)
    {
        currentPage = Mathf.Clamp(pageIndex, 0, totalPages - 1);
        
        // Change the sprite based on current page
        if (manualPageImage != null)
        {
            switch (currentPage)
            {
                case 0:
                    manualPageImage.sprite = page1_Wires;
                    break;
                case 1:
                    manualPageImage.sprite = page2_Button;
                    break;
                case 2:
                    manualPageImage.sprite = page3_Memory;
                    break;
            }
        }
        
        // Update page number text
        if (pageNumberText != null)
            pageNumberText.text = $"{currentPage + 1} / {totalPages}";
        
        // Update button states
        if (prevPageButton != null)
            prevPageButton.interactable = (currentPage > 0);
        if (nextPageButton != null)
            nextPageButton.interactable = (currentPage < totalPages - 1);
    }
    
    public void NextPage()
    {
        if (currentPage < totalPages - 1)
        {
            PlaySound(pageFlipSound);
            ShowPage(currentPage + 1);
        }
    }
    
    public void PrevPage()
    {
        if (currentPage > 0)
        {
            PlaySound(pageFlipSound);
            ShowPage(currentPage - 1);
        }
    }
    
    // ==================== PLAYER CONTROL MANAGEMENT ====================
    
    void DisablePlayerControls()
    {
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;
        
        // Try to find and disable ThirdPersonController
        cachedPlayerController = localPlayer.GetComponent<StarterAssets.ThirdPersonController>();
        
        if (cachedPlayerController == null)
            cachedPlayerController = localPlayer.GetComponent("ThirdPersonController") as MonoBehaviour;
        
        if (cachedPlayerController == null)
            cachedPlayerController = localPlayer.GetComponent("CustomCharacterController.ThirdPersonController") as MonoBehaviour;
        
        if (cachedPlayerController != null)
        {
            cachedPlayerController.enabled = false;
        }
    }
    
    void EnablePlayerControls()
    {
        if (cachedPlayerController != null)
        {
            cachedPlayerController.enabled = true;
            cachedPlayerController = null;
        }
    }
    
    GameObject FindLocalPlayer()
    {
        // Method 1: PhotonNetwork TagObject
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer.TagObject != null)
        {
            return PhotonNetwork.LocalPlayer.TagObject as GameObject;
        }
        
        // Method 2: Find by tag with PhotonView check
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            PhotonView pv = p.GetComponent<PhotonView>();
            if (pv == null || pv.IsMine)
            {
                return p;
            }
        }
        
        return null;
    }
    
    // ==================== HELPERS ====================
    
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
    
    public bool HasManual() => hasManual;
    public bool IsOpen() => isOpen;
    
    public void SetCanToggle(bool canView) { }
    
    public static void ResetState()
    {
        hasManual = false;
    }
}