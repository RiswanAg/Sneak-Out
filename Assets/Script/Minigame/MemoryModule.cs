using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MemoryModule.cs - Memory sequence puzzle
/// Player must press buttons based on display number and previous stages
/// 3 stages - must remember positions AND labels from previous stages
/// </summary>
public class MemoryModule : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modulePanel;
    public TMP_Text displayText;           // Shows number 1-4
    public Button[] memoryButtons;         // 4 buttons labeled 1-4
    public TMP_Text[] buttonLabels;        // Text on each button
    public TMP_Text stageText;             // "Peringkat 1/3"
    public Image[] stageIndicators;        // 3 lights showing progress
    
    [Header("Colors")]
    public Color stageIncomplete = Color.gray;
    public Color stageComplete = Color.green;
    public Color stageCurrent = Color.yellow;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip buttonClickSound;
    public AudioClip stageCompleteSound;
    public AudioClip wrongSound;
    
    [Header("Animation")]
    public float displayFlashDuration = 0.5f;
    
    // Game state
    private bool isActive = false;
    private bool isComplete = false;
    private int currentStage = 0;          // 0, 1, 2 (3 stages)
    private int displayNumber;             // Current display (1-4)
    
    // Memory tracking - CRUCIAL for the puzzle!
    private List<int> pressedPositions = new List<int>();  // Position pressed each stage (0-3)
    private List<int> pressedLabels = new List<int>();     // Label pressed each stage (1-4)
    
    // Button arrangement - labels are shuffled each stage
    private int[] currentButtonLabels = new int[4];        // Which label (1-4) is at each position
    
    void Start()
    {
        // Setup button click handlers
        for (int i = 0; i < memoryButtons.Length; i++)
        {
            int buttonPosition = i; // Capture for closure
            memoryButtons[i].onClick.RemoveAllListeners();
            memoryButtons[i].onClick.AddListener(() => OnButtonPressed(buttonPosition));
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
        if (modulePanel != null)
            modulePanel.SetActive(active);
    }
    
    /// <summary>
    /// Initialize the memory puzzle
    /// </summary>
    public void Initialize()
    {
        isComplete = false;
        currentStage = 0;
        pressedPositions.Clear();
        pressedLabels.Clear();
        
        // Setup first stage
        SetupStage();
        UpdateStageUI();
        
        Debug.Log("=== MEMORY MODULE INITIALIZED ===");
    }
    
    /// <summary>
    /// Setup a new stage with random display and shuffled buttons
    /// </summary>
    void SetupStage()
    {
        // Random display number (1-4)
        displayNumber = Random.Range(1, 5);
        
        if (displayText != null)
        {
            displayText.text = displayNumber.ToString();
            StartCoroutine(FlashDisplay());
        }
        
        // Shuffle button labels (1, 2, 3, 4 in random positions)
        ShuffleButtonLabels();
        
        // Update button UI
        for (int i = 0; i < memoryButtons.Length; i++)
        {
            if (buttonLabels[i] != null)
            {
                buttonLabels[i].text = currentButtonLabels[i].ToString();
            }
            memoryButtons[i].interactable = true;
        }
        
        // Debug log
        Debug.Log($"=== STAGE {currentStage + 1} ===");
        Debug.Log($"Display: {displayNumber}");
        Debug.Log($"Button labels (left to right): {currentButtonLabels[0]}, {currentButtonLabels[1]}, {currentButtonLabels[2]}, {currentButtonLabels[3]}");
        
        int correctPos = GetCorrectPosition();
        Debug.Log($"Correct position: {correctPos + 1} (label: {currentButtonLabels[correctPos]})");
    }
    
    void ShuffleButtonLabels()
    {
        // Create array [1, 2, 3, 4]
        List<int> labels = new List<int> { 1, 2, 3, 4 };
        
        // Fisher-Yates shuffle
        for (int i = labels.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = labels[i];
            labels[i] = labels[j];
            labels[j] = temp;
        }
        
        // Assign to positions
        for (int i = 0; i < 4; i++)
        {
            currentButtonLabels[i] = labels[i];
        }
    }
    
    IEnumerator FlashDisplay()
    {
        // Flash the display to draw attention
        Color originalColor = displayText.color;
        displayText.color = Color.yellow;
        yield return new WaitForSeconds(displayFlashDuration);
        displayText.color = originalColor;
    }
    
    // ==================== BUTTON PRESS HANDLING ====================
    
    void OnButtonPressed(int position)
    {
        if (!isActive || isComplete) return;
        
        int pressedLabel = currentButtonLabels[position];
        
        Debug.Log($"Pressed position {position + 1}, label {pressedLabel}");
        
        // Play click sound
        if (audioSource != null && buttonClickSound != null)
            audioSource.PlayOneShot(buttonClickSound);
        
        // Check if correct
        int correctPosition = GetCorrectPosition();
        
        if (position == correctPosition)
        {
            // CORRECT!
            Debug.Log("Correct!");
            
            // Remember what was pressed
            pressedPositions.Add(position);
            pressedLabels.Add(pressedLabel);
            
            // Play stage complete sound
            if (audioSource != null && stageCompleteSound != null)
                audioSource.PlayOneShot(stageCompleteSound);
            
            // Move to next stage or complete
            currentStage++;
            UpdateStageUI();
            
            if (currentStage >= 3)
            {
                // ALL STAGES COMPLETE!
                Debug.Log("MEMORY MODULE COMPLETE!");
                isComplete = true;
                
                // Disable buttons
                foreach (Button btn in memoryButtons)
                    btn.interactable = false;
                
                // Notify manager
                if (ControlPanelManager.Instance != null)
                {
                    ControlPanelManager.Instance.ModuleComplete(2); // Module 2 = Memory
                }
            }
            else
            {
                // Setup next stage
                SetupStage();
            }
        }
        else
        {
            // WRONG!
            Debug.Log($"Wrong! Expected position {correctPosition + 1}");
            
            if (audioSource != null && wrongSound != null)
                audioSource.PlayOneShot(wrongSound);
            
            // Add strike
            if (ControlPanelManager.Instance != null)
            {
                ControlPanelManager.Instance.AddStrike("Butang memori salah!");
            }
            
            // RESET to stage 1 (harsh but that's the game!)
            currentStage = 0;
            pressedPositions.Clear();
            pressedLabels.Clear();
            
            UpdateStageUI();
            SetupStage();
        }
    }
    
    // ==================== RULE CALCULATIONS ====================
    
    /// <summary>
    /// Calculate correct button position based on stage and display
    /// </summary>
    int GetCorrectPosition()
    {
        switch (currentStage)
        {
            case 0: return GetStage1Position();
            case 1: return GetStage2Position();
            case 2: return GetStage3Position();
            default: return 0;
        }
    }
    
    // STAGE 1 RULES
    int GetStage1Position()
    {
        switch (displayNumber)
        {
            case 1: return 1; // Position 2 (index 1)
            case 2: return 1; // Position 2 (index 1)
            case 3: return 2; // Position 3 (index 2)
            case 4: return 3; // Position 4 (index 3)
            default: return 0;
        }
    }
    
    // STAGE 2 RULES
    int GetStage2Position()
    {
        switch (displayNumber)
        {
            case 1:
                // Press button labeled "4"
                return GetPositionOfLabel(4);
                
            case 2:
                // Press same POSITION as stage 1
                return pressedPositions[0];
                
            case 3:
                // Press position 1 (index 0)
                return 0;
                
            case 4:
                // Press same POSITION as stage 1
                return pressedPositions[0];
                
            default: return 0;
        }
    }
    
    // STAGE 3 RULES
    int GetStage3Position()
    {
        switch (displayNumber)
        {
            case 1:
                // Press button with same LABEL as stage 2
                return GetPositionOfLabel(pressedLabels[1]);
                
            case 2:
                // Press button with same LABEL as stage 1
                return GetPositionOfLabel(pressedLabels[0]);
                
            case 3:
                // Press position 3 (index 2)
                return 2;
                
            case 4:
                // Press button labeled "4"
                return GetPositionOfLabel(4);
                
            default: return 0;
        }
    }
    
    /// <summary>
    /// Find which position has a specific label
    /// </summary>
    int GetPositionOfLabel(int label)
    {
        for (int i = 0; i < currentButtonLabels.Length; i++)
        {
            if (currentButtonLabels[i] == label)
                return i;
        }
        Debug.LogError($"Label {label} not found!");
        return 0;
    }
    
    // ==================== UI UPDATES ====================
    
    void UpdateStageUI()
    {
        // Update stage text
        if (stageText != null)
        {
            stageText.text = $"Peringkat {currentStage + 1} / 3";
        }
        
        // Update stage indicators
        for (int i = 0; i < stageIndicators.Length; i++)
        {
            if (stageIndicators[i] != null)
            {
                if (i < currentStage)
                    stageIndicators[i].color = stageComplete;    // Completed
                else if (i == currentStage)
                    stageIndicators[i].color = stageCurrent;     // Current
                else
                    stageIndicators[i].color = stageIncomplete;  // Not yet
            }
        }
    }
    
    // Public getters for debugging
    public int GetCurrentStage() => currentStage;
    public int GetDisplayNumber() => displayNumber;
    public int[] GetButtonLabels() => (int[])currentButtonLabels.Clone();
}

/* ============================================================
   HOW IT WORKS - EXPLANATION
   ============================================================
   
   This is the trickiest module! The player must remember:
   - POSITION they pressed (1st, 2nd, 3rd, 4th button from left)
   - LABEL they pressed (1, 2, 3, or 4)
   
   Button labels are SHUFFLED each stage, so position != label!
   
   STAGE 1 RULES (based on display number):
   - Display "1" → Press button in POSITION 2
   - Display "2" → Press button in POSITION 2
   - Display "3" → Press button in POSITION 3
   - Display "4" → Press button in POSITION 4
   
   STAGE 2 RULES:
   - Display "1" → Press button LABELED "4" (find it!)
   - Display "2" → Press same POSITION as Stage 1
   - Display "3" → Press button in POSITION 1
   - Display "4" → Press same POSITION as Stage 1
   
   STAGE 3 RULES:
   - Display "1" → Press button with same LABEL as Stage 2
   - Display "2" → Press button with same LABEL as Stage 1
   - Display "3" → Press button in POSITION 3
   - Display "4" → Press button LABELED "4"
   
   EXAMPLE PLAYTHROUGH:
   Stage 1: Display=2 → Press position 2 (say label was "3")
            Remember: Position=2, Label=3
   
   Stage 2: Display=4 → Press same position as Stage 1 (position 2)
            New button there might be labeled "1"
            Remember: Position=2, Label=1
   
   Stage 3: Display=1 → Press button with same LABEL as Stage 2
            Stage 2 label was "1", find where "1" is now!
   
   COORDINATOR TIPS:
   - Write down position AND label after each stage
   - "Position 2, label was 3"
   - Stage 3 often asks about previous labels!
   
   DIFFICULTY ADJUSTMENTS:
   - Easier: Only 2 stages, simpler rules
   - Harder: 5 stages like original game
   
   ============================================================
*/
