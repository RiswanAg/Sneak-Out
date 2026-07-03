using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }
    
    [Header("UI References")]
    [Tooltip("Game Over panel/screen")]
    public GameObject gameOverPanel;
    
    [Tooltip("Optional: Text to show message")]
    public Text gameOverText;
    
    [Tooltip("Optional: Retry button")]
    public Button retryButton;
    
    [Tooltip("Optional: Main menu button")]
    public Button mainMenuButton;
    
    [Header("Settings")]
    [Tooltip("Time before showing game over UI")]
    public float delayBeforeUI = 1f;
    
    [Tooltip("Scene name for main menu")]
    public string mainMenuScene = "MainMenu";
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip gameOverSound;
    
    [Header("Animation")]
    [Tooltip("Optional: Animator for game over screen")]
    public Animator gameOverAnimator;
    
    private bool isGameOver = false;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Hide game over panel at start
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Setup buttons
        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);
    }
    
    void Start()
    {
        // Subscribe to Cikgu event
        CikguNPC.OnPlayerCaught += OnPlayerCaught;
        
        // Reset StudentNPC game over flag when scene loads
        ResetStudentNPCFlags();
    }
    
    void OnDestroy()
    {
        CikguNPC.OnPlayerCaught -= OnPlayerCaught;
    }
    
    void OnPlayerCaught(GameObject player)
    {
        Debug.Log("[GameOver] Player caught by Cikgu!");
    }
    
    /// <summary>
    /// Call this to trigger game over
    /// </summary>
    public void TriggerGameOver()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        Debug.Log("[GameOver] GAME OVER!");
        
        StartCoroutine(GameOverSequence());
    }
    
    IEnumerator GameOverSequence()
    {
        // Play sound
        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);
        
        // Wait for delay (let sleeping student wake animation play)
        yield return new WaitForSeconds(delayBeforeUI);
        
        // Pause game
        Time.timeScale = 0f;
        
        // Show game over panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            
            // Play animation if exists
            if (gameOverAnimator != null)
                gameOverAnimator.SetTrigger("Show");
        }
        
        // Unlock cursor for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Retry current level
    /// </summary>
    public void RetryLevel()
    {
        Debug.Log("[GameOver] Retrying level...");
        
        // Reset time scale
        Time.timeScale = 1f;
        
        // Reset game over state
        isGameOver = false;
        
        // IMPORTANT: Reset StudentNPC static flags before reloading
        ResetStudentNPCFlags();
        
        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Go to main menu
    /// </summary>
    public void GoToMainMenu()
    {
        Debug.Log("[GameOver] Going to main menu...");
        
        // Reset time scale
        Time.timeScale = 1f;
        
        // Reset StudentNPC flags
        ResetStudentNPCFlags();
        
        // Load main menu
        SceneManager.LoadScene(mainMenuScene);
    }
    
    /// <summary>
    /// Check if game is over
    /// </summary>
    public bool IsGameOver()
    {
        return isGameOver;
    }
    
    /// <summary>
    /// Reset game over state (for testing)
    /// </summary>
    public void ResetGameOver()
    {
        isGameOver = false;
        Time.timeScale = 1f;
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        ResetStudentNPCFlags();
    }
    
    /// <summary>
    /// Reset StudentNPC static flags using reflection
    /// </summary>
    private void ResetStudentNPCFlags()
    {
        // Use reflection to reset the static gameOverTriggered flag
        var studentNPCType = System.Type.GetType("StudentNPC");
        if (studentNPCType != null)
        {
            var field = studentNPCType.GetField("gameOverTriggered", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Static);
            
            if (field != null)
            {
                field.SetValue(null, false);
                Debug.Log("[GameOver] Reset StudentNPC gameOverTriggered flag");
            }
        }
    }
}