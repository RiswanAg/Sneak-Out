using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// KeypadModule.cs - Symbol/Keypad puzzle (UPDATED - EXACT MANUAL MATCH)
/// 
/// MODUL 2: SIMBOL
/// - 4 symbols displayed on buttons
/// - Find the column containing ALL 4 symbols
/// - Press in order from TOP to BOTTOM
/// 
/// Based on SIMBOL__1_.png manual
/// </summary>
public class KeypadModule : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modulePanel;
    public Button[] symbolButtons;           // 4 buttons
    public Image[] symbolImages;             // Image components for sprites
    public TMP_Text[] symbolTexts;           // Text fallback
    public TMP_Text instructionText;
    
    [Header("Symbol Sprites (19 Symbols)")]
    [Tooltip("Assign 19 symbol sprites matching the manual order")]
    public Sprite[] symbolSprites;
    
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color pressedColor = new Color(0.2f, 0.8f, 0.2f);
    public Color wrongColor = new Color(0.8f, 0.2f, 0.2f);
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pressSound;
    public AudioClip correctSound;
    public AudioClip wrongSound;
    
    // ==================== SYMBOL MAPPING (UPDATED ORDER) ====================
    // Based on your exact sprite order
    //
    // Sprite Index | Symbol | Description
    // -------------|--------|------------------
    //      0       |   ©    | Copyright
    //      1       |   ¶    | Pilcrow/Paragraph
    //      2       |   ¿    | Inverted question mark
    //      3       |   æ    | AE ligature
    //      4       |   Ψ    | Psi
    //      5       |   Ω    | Omega
    //      6       |   ϗ    | Kai (K-like)
    //      7       |   Ϙ    | Koppa
    //      8       |   Ϟ    | Koppa variant
    //      9       |   Ϭ    | Shima
    //      10      |   Ͼ    | Lunate Sigma / C
    //      11      |   Ѧ    | Little Yus / A
    //      12      |   Ѫ    | Big Yus / @
    //      13      |   Ѭ    | Iotified Big Yus
    //      14      |   Ѯ    | Ksi / LX
    //      15      |   Ҁ    | Koppa variant / Crescent
    //      16      |   Ҩ    | Abkhasian Ha / Omega variant
    //      17      |   Ԇ    | Komi De
    //      18      |   ☆    | Star
    
    private static readonly string[] SYMBOL_CHARS = {
        "©",   // 0  - Copyright
        "¶",   // 1  - Pilcrow
        "¿",   // 2  - Inverted question mark
        "æ",   // 3  - AE ligature
        "Ψ",   // 4  - Psi
        "Ω",   // 5  - Omega
        "ϗ",   // 6  - Kai
        "Ϙ",   // 7  - Koppa
        "Ϟ",   // 8  - Koppa variant
        "Ϭ",   // 9  - Shima
        "Ͼ",   // 10 - Lunate Sigma / C
        "Ѧ",   // 11 - Little Yus / A
        "Ѫ",   // 12 - Big Yus / @
        "Ѭ",   // 13 - Iotified Big Yus
        "Ѯ",   // 14 - Ksi / LX
        "Ҁ",   // 15 - Crescent
        "Ҩ",   // 16 - Omega variant
        "Ԇ",   // 17 - Komi De
        "☆"    // 18 - Star
    };
    
    // ==================== 6 COLUMNS (EXACT FROM MANUAL) ====================
    // User-provided column data mapped to sprite order: ©,¶,¿,æ,Ψ,Ω,ϗ,Ϙ,Ϟ,Ϭ,Ͼ,Ѧ,Ѫ,Ѭ,Ѯ,Ҁ,Ҩ,Ԇ,☆
    //
    // Index mapping:
    // ©=0, ¶=1, ¿=2, æ=3, Ψ=4, Ω=5, ϗ=6, Ϙ=7, Ϟ=8, Ϭ=9, Ͼ=10, Ѧ=11, Ѫ=12, Ѭ=13, Ѯ=14, Ҁ=15, Ҩ=16, Ԇ=17, ☆=18
    
    private static readonly int[][] COLUMNS = {
        // LAJUR 1: ¿, Ѧ, Ϟ, Ѫ, Ͼ, Ϙ
        new int[] { 2, 11, 8, 12, 10, 7 },
        
        // LAJUR 2: Ԇ, Ѭ, Ҁ, Ͼ, б(use Ϭ), ☆
        new int[] { 17, 13, 15, 10, 9, 18 },
        
        // LAJUR 3: ©, æ, Ѭ, Ψ, Ѫ, ☆
        new int[] { 0, 3, 13, 4, 12, 18 },
        
        // LAJUR 4: ϗ, ¶, Ҁ, Ѫ, Ϟ, Ҩ
        new int[] { 6, 1, 15, 12, 8, 16 },
        
        // LAJUR 5: Ψ, Ҩ, ¶, Ѯ, Ҁ, Ͼ
        new int[] { 4, 16, 1, 14, 15, 10 },
        
        // LAJUR 6: ϗ, Ԇ, Ѯ, æ, Ψ, Ω
        new int[] { 6, 17, 14, 3, 4, 5 }
    };
    
    // Game state
    private bool isActive = false;
    private bool isComplete = false;
    private int[] displayedSymbols;          // 4 symbols shown on buttons
    private int correctColumn;               // Which column contains all 4
    private int[] correctOrder;              // Order to press (button indices 0-3)
    private int currentPressIndex = 0;
    private bool[] buttonPressed;
    
    void Start()
    {
        // Setup button click handlers
        for (int i = 0; i < symbolButtons.Length; i++)
        {
            int buttonIndex = i;
            symbolButtons[i].onClick.AddListener(() => OnButtonPressed(buttonIndex));
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
        if (modulePanel != null)
            modulePanel.SetActive(active);
    }
    
    public void Initialize()
    {
        isComplete = false;
        currentPressIndex = 0;
        buttonPressed = new bool[4];
        
        GeneratePuzzle();
        SetupUI();
        
        GameLog.Log($"<color=cyan>=== SYMBOL MODULE (MODUL 2: SIMBOL) ===</color>");
        string symbolsStr = "";
        for (int i = 0; i < 4; i++)
        {
            symbolsStr += SYMBOL_CHARS[displayedSymbols[i]] + " ";
        }
        GameLog.Log($"<color=cyan>4 Symbols displayed: {symbolsStr}</color>");
        GameLog.Log($"<color=cyan>Found in LAJUR (Column): {correctColumn + 1}</color>");
        GameLog.Log($"<color=lime>Press order (ATAS ke BAWAH): Button {correctOrder[0]+1} → {correctOrder[1]+1} → {correctOrder[2]+1} → {correctOrder[3]+1}</color>");
    }
    
    void GeneratePuzzle()
    {
        // Pick a random column
        correctColumn = Random.Range(0, 6);
        int[] column = COLUMNS[correctColumn];
        
        // Pick 4 random symbols from this column
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < column.Length; i++)
            availableIndices.Add(i);
        
        // Shuffle and pick 4
        List<int> pickedColumnIndices = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int randIndex = Random.Range(0, availableIndices.Count);
            pickedColumnIndices.Add(availableIndices[randIndex]);
            availableIndices.RemoveAt(randIndex);
        }
        
        // Sort to maintain column order (top to bottom)
        pickedColumnIndices.Sort();
        
        // Get actual symbol indices
        displayedSymbols = new int[4];
        for (int i = 0; i < 4; i++)
        {
            displayedSymbols[i] = column[pickedColumnIndices[i]];
        }
        
        // Now shuffle for display (so they don't appear in order)
        List<int> displayOrder = new List<int> { 0, 1, 2, 3 };
        for (int i = displayOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = displayOrder[i];
            displayOrder[i] = displayOrder[j];
            displayOrder[j] = temp;
        }
        
        // Create shuffled display
        int[] shuffledSymbols = new int[4];
        for (int i = 0; i < 4; i++)
        {
            shuffledSymbols[i] = displayedSymbols[displayOrder[i]];
        }
        displayedSymbols = shuffledSymbols;
        
        // Calculate correct press order (based on column position)
        CalculateCorrectOrder();
    }
    
    void CalculateCorrectOrder()
    {
        // For each displayed symbol, find its position in the correct column
        // Then sort by column position (top to bottom)
        
        int[] column = COLUMNS[correctColumn];
        
        List<(int symbolIndex, int columnPos, int buttonIndex)> symbolData = new List<(int, int, int)>();
        
        for (int i = 0; i < 4; i++)
        {
            int symbolIndex = displayedSymbols[i];
            
            // Find position in column
            int columnPos = -1;
            for (int j = 0; j < column.Length; j++)
            {
                if (column[j] == symbolIndex)
                {
                    columnPos = j;
                    break;
                }
            }
            
            symbolData.Add((symbolIndex, columnPos, i));
        }
        
        // Sort by column position (top to bottom)
        symbolData = symbolData.OrderBy(x => x.columnPos).ToList();
        
        // Extract button order
        correctOrder = new int[4];
        for (int i = 0; i < 4; i++)
        {
            correctOrder[i] = symbolData[i].buttonIndex;
        }
    }
    
    void SetupUI()
    {
        for (int i = 0; i < symbolButtons.Length; i++)
        {
            // Reset button
            symbolButtons[i].interactable = true;
            
            if (symbolImages[i] != null)
            {
                symbolImages[i].color = normalColor;
            }
            
            // Set symbol
            int symbolIndex = displayedSymbols[i];
            
            // Use sprite if available
            if (symbolSprites != null && symbolIndex < symbolSprites.Length && symbolSprites[symbolIndex] != null)
            {
                if (symbolImages[i] != null)
                {
                    symbolImages[i].sprite = symbolSprites[symbolIndex];
                    symbolImages[i].color = normalColor;
                }
                
                // Hide text if using sprite
                if (symbolTexts[i] != null)
                    symbolTexts[i].text = "";
            }
            else
            {
                // Fallback to text
                if (symbolTexts[i] != null)
                {
                    symbolTexts[i].text = SYMBOL_CHARS[symbolIndex];
                    symbolTexts[i].fontSize = 72;
                }
            }
        }
        
        if (instructionText != null)
            instructionText.text = "Tekan mengikut urutan (ATAS ke BAWAH)";
    }
    
    void OnButtonPressed(int buttonIndex)
    {
        if (!isActive || isComplete || buttonPressed[buttonIndex]) return;
        
        GameLog.Log($"<color=yellow>Button {buttonIndex + 1} pressed (Symbol: {SYMBOL_CHARS[displayedSymbols[buttonIndex]]})</color>");
        
        if (audioSource != null && pressSound != null)
            audioSource.PlayOneShot(pressSound);
        
        // Check if correct
        if (buttonIndex == correctOrder[currentPressIndex])
        {
            // CORRECT!
            GameLog.Log($"<color=lime>✓ Correct! ({currentPressIndex + 1}/4)</color>");
            
            buttonPressed[buttonIndex] = true;
            
            // Visual feedback - green
            if (symbolImages[buttonIndex] != null)
            {
                symbolImages[buttonIndex].color = pressedColor;
            }
            
            symbolButtons[buttonIndex].interactable = false;
            
            currentPressIndex++;
            
            // Check if complete
            if (currentPressIndex >= 4)
            {
                GameLog.Log("<color=lime>★★★ SYMBOL MODULE COMPLETE! ★★★</color>");
                isComplete = true;
                
                if (audioSource != null && correctSound != null)
                    audioSource.PlayOneShot(correctSound);
                
                if (instructionText != null)
                    instructionText.text = "BETUL!";
                
                // Notify manager
                if (ControlPanelManager.Instance != null)
                {
                    ControlPanelManager.Instance.ModuleComplete(3); // Module 3 = Symbols
                }
            }
        }
        else
        {
            // WRONG!
            GameLog.Log($"<color=red>✗ Wrong! Expected button {correctOrder[currentPressIndex] + 1}</color>");
            
            if (audioSource != null && wrongSound != null)
                audioSource.PlayOneShot(wrongSound);
            
            // Flash red on wrong button
            if (symbolImages[buttonIndex] != null)
            {
                StartCoroutine(FlashWrong(buttonIndex));
            }
            
            // Add strike
            if (ControlPanelManager.Instance != null)
            {
                ControlPanelManager.Instance.AddStrike("Simbol salah!");
            }
            
            // Reset progress
            ResetModule();
        }
    }
    
    System.Collections.IEnumerator FlashWrong(int buttonIndex)
    {
        // Flash red
        if (symbolImages[buttonIndex] != null)
        {
            symbolImages[buttonIndex].color = wrongColor;
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Return to normal
        if (symbolImages[buttonIndex] != null)
        {
            symbolImages[buttonIndex].color = normalColor;
        }
    }
    
    void ResetModule()
    {
        currentPressIndex = 0;
        
        for (int i = 0; i < 4; i++)
        {
            buttonPressed[i] = false;
            symbolButtons[i].interactable = true;
            
            if (symbolImages[i] != null)
            {
                symbolImages[i].color = normalColor;
            }
        }
        
        if (instructionText != null)
            instructionText.text = "Cuba lagi! Tekan mengikut urutan.";
        
        GameLog.Log("<color=orange>Module RESET - Try again!</color>");
    }
    
    // Public getters for debugging
    public int GetCorrectColumn() => correctColumn;
    public int[] GetDisplayedSymbols() => displayedSymbols;
    public int[] GetCorrectOrder() => correctOrder;
}

/* ============================================================
   HOW IT WORKS - MODUL 2: SIMBOL
   ============================================================
   
   FROM MANUAL (SIMBOL__1_.png):
   - 4 simbol dipaparkan pada butang
   - Cari LAJUR yang ada SEMUA 4 simbol
   - Tekan mengikut urutan (ATAS ke BAWAH)
   
   EXAMPLE from manual:
   Symbols shown: © Ж ☆ æ
   
   These are all in LAJUR 3 (Column 3):
   Position 1: © (top)
   Position 2: æ
   Position 3: LX (not shown)
   Position 4: Ψ (not shown)
   Position 5: Ж
   Position 6: ☆ (bottom)
   
   Correct press order (top to bottom):
   1. Press button with ©
   2. Press button with æ
   3. Press button with Ж
   4. Press button with ☆
   
   ============================================================
*/