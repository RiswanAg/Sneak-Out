using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// WiresModule.cs - Wire cutting puzzle (FIXED VERSION v2)
/// 
/// FIXES:
/// - Disables Button color transitions (Transition = None)
/// - Disables WireVisual script hover effects
/// - Colors stay consistent - no changes on hover
/// </summary>
public class WiresModule : MonoBehaviour
{
    [Header("Wire UI Elements")]
    public GameObject modulePanel;
    public Button[] wireButtons;
    public Image[] wireImages;          // The child WireImage objects
    
    [Header("Wire Colors")]
    public Color redColor = new Color(1f, 0.2f, 0.2f);
    public Color blueColor = new Color(0.2f, 0.4f, 1f);
    public Color whiteColor = Color.white;
    public Color yellowColor = new Color(1f, 0.9f, 0.2f);
    public Color cutWireColor = new Color(0.3f, 0.3f, 0.3f);
    
    [Header("Settings")]
    [Range(3, 4)]
    public int numberOfWires = 3;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip cutSound;
    public AudioClip wrongSound;
    
    // Wire data
    public enum WireColor { Red, Blue, White, Yellow }
    private List<WireColor> currentWires = new List<WireColor>();
    private int correctWireIndex = -1;
    private bool isActive = false;
    private bool isComplete = false;
    
    public void SetActive(bool active)
    {
        isActive = active;
        if (modulePanel != null)
            modulePanel.SetActive(active);
    }
    
    public void Initialize()
    {
        isComplete = false;
        currentWires.Clear();
        
        numberOfWires = Random.Range(3, 5);
        
        WireColor[] possibleColors = { WireColor.Red, WireColor.Blue, WireColor.White, WireColor.Yellow };
        
        for (int i = 0; i < numberOfWires; i++)
        {
            WireColor randomColor = possibleColors[Random.Range(0, possibleColors.Length)];
            currentWires.Add(randomColor);
        }
        
        correctWireIndex = CalculateCorrectWire();
        SetupWireUI();
        
        Debug.Log($"<color=cyan>=== WIRES MODULE ===</color>");
        Debug.Log($"<color=cyan>Wire count: {numberOfWires}</color>");
        for (int i = 0; i < currentWires.Count; i++)
        {
            Debug.Log($"<color=cyan>Wire {i + 1}: {currentWires[i]}</color>");
        }
        Debug.Log($"<color=lime>Correct wire to cut: {correctWireIndex + 1}</color>");
    }
    
    void SetupWireUI()
    {
        for (int i = 0; i < wireButtons.Length; i++)
        {
            if (i < numberOfWires)
            {
                wireButtons[i].gameObject.SetActive(true);
                wireButtons[i].interactable = true;
                
                Color wireColor = GetUnityColor(currentWires[i]);
                
                // ✅ Set color on WireImage
                if (wireImages != null && i < wireImages.Length && wireImages[i] != null)
                {
                    wireImages[i].color = wireColor;
                }
                
                // ✅ Disable WireVisual script (causes hover effect)
                DisableWireVisual(wireButtons[i].gameObject);
                
                // ✅ Disable Button color transition
                wireButtons[i].transition = Selectable.Transition.None;
                
                // ✅ Make button image transparent
                Image buttonImage = wireButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = new Color(1, 1, 1, 0);
                }
                
                int wireIndex = i;
                wireButtons[i].onClick.RemoveAllListeners();
                wireButtons[i].onClick.AddListener(() => OnWireCut(wireIndex));
            }
            else
            {
                wireButtons[i].gameObject.SetActive(false);
            }
        }
    }
    
    void DisableWireVisual(GameObject wireButtonObj)
    {
        // Disable any WireVisual script in children
        MonoBehaviour[] scripts = wireButtonObj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in scripts)
        {
            if (script != null && script.GetType().Name == "WireVisual")
            {
                script.enabled = false;
                Debug.Log($"<color=orange>Disabled WireVisual on {wireButtonObj.name}</color>");
            }
        }
    }
    
    Color GetUnityColor(WireColor wireColor)
    {
        switch (wireColor)
        {
            case WireColor.Red: return redColor;
            case WireColor.Blue: return blueColor;
            case WireColor.White: return whiteColor;
            case WireColor.Yellow: return yellowColor;
            default: return Color.gray;
        }
    }
    
    void OnWireCut(int wireIndex)
    {
        if (!isActive || isComplete) return;
        
        Debug.Log($"<color=orange>Cut wire {wireIndex + 1} ({currentWires[wireIndex]})</color>");
        
        if (wireImages != null && wireIndex < wireImages.Length && wireImages[wireIndex] != null)
        {
            wireImages[wireIndex].color = cutWireColor;
        }
        
        wireButtons[wireIndex].interactable = false;
        
        if (audioSource != null && cutSound != null)
            audioSource.PlayOneShot(cutSound);
        
        if (wireIndex == correctWireIndex)
        {
            Debug.Log("<color=lime>✓ Correct wire cut!</color>");
            isComplete = true;
            
            if (ControlPanelManager.Instance != null)
                ControlPanelManager.Instance.ModuleComplete(0);
        }
        else
        {
            Debug.Log("<color=red>✗ Wrong wire! STRIKE!</color>");
            
            if (audioSource != null && wrongSound != null)
                audioSource.PlayOneShot(wrongSound);
            
            if (ControlPanelManager.Instance != null)
                ControlPanelManager.Instance.AddStrike("Wayar salah dipotong!");
        }
    }
    
    int CalculateCorrectWire()
    {
        if (numberOfWires == 3)
            return CalculateCorrectWire3();
        else
            return CalculateCorrectWire4();
    }
    
    int CalculateCorrectWire3()
    {
        if (!HasColor(WireColor.Red))
            return 1;
        
        if (currentWires[currentWires.Count - 1] == WireColor.White)
            return currentWires.Count - 1;
        
        if (CountColor(WireColor.Blue) > 1)
            return GetLastIndexOfColor(WireColor.Blue);
        
        return currentWires.Count - 1;
    }
    
    int CalculateCorrectWire4()
    {
        if (CountColor(WireColor.Red) > 1)
            return GetLastIndexOfColor(WireColor.Red);
        
        if (currentWires[currentWires.Count - 1] == WireColor.Yellow && !HasColor(WireColor.Red))
            return 0;
        
        if (CountColor(WireColor.Blue) == 1)
            return 0;
        
        if (CountColor(WireColor.Yellow) > 1)
            return currentWires.Count - 1;
        
        return 1;
    }
    
    bool HasColor(WireColor color) => currentWires.Contains(color);
    
    int CountColor(WireColor color)
    {
        int count = 0;
        foreach (WireColor wire in currentWires)
            if (wire == color) count++;
        return count;
    }
    
    int GetLastIndexOfColor(WireColor color)
    {
        for (int i = currentWires.Count - 1; i >= 0; i--)
            if (currentWires[i] == color) return i;
        return -1;
    }
    
    public int GetWireCount() => numberOfWires;
    public List<WireColor> GetWires() => new List<WireColor>(currentWires);
    public int GetCorrectWire() => correctWireIndex;
}