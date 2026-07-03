using UnityEngine;
using System.Collections;

/// <summary>
/// Washing Machine Distraction Mechanic
/// - Player sets a timer (e.g., 5 seconds)
/// - After timer, washing machine turns ON
/// - Makes LOUD sound that attracts Cikgu/NPCs
/// - Player can escape while NPCs investigate the washing machine
/// </summary>
public class WashingMachine : MonoBehaviour
{
    [Header("=== WASHING MACHINE SETTINGS ===")]
    [Tooltip("Is the washing machine currently running?")]
    public bool isRunning = false;
    
    [Header("Timer Settings")]
    [Tooltip("Default timer delay before machine turns on (seconds)")]
    public float defaultTimerDelay = 5f;
    
    [Tooltip("Minimum timer delay player can set")]
    public float minTimerDelay = 3f;
    
    [Tooltip("Maximum timer delay player can set")]
    public float maxTimerDelay = 30f;
    
    [Tooltip("How long the machine runs before turning off (seconds)")]
    public float runDuration = 15f;
    
    [Header("Sound Settings")]
    [Tooltip("How often to emit sound while running (seconds)")]
    public float soundEmitInterval = 2f;
    
    [Tooltip("Sound type to emit (attracts NPCs)")]
    public SoundType soundType = SoundType.Loud;
    
    [Tooltip("Room ID for sound detection (0 = auto-detect)")]
    public int roomID = 0;
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    [Tooltip("Sound when timer is set (beep)")]
    public AudioClip timerSetSound;
    
    [Tooltip("Sound when machine starts")]
    public AudioClip startSound;
    
    [Tooltip("Looping sound while running")]
    public AudioClip runningLoopSound;
    
    [Tooltip("Sound when machine stops")]
    public AudioClip stopSound;
    
    [Range(0f, 1f)]
    public float volume = 0.8f;
    
    [Header("Visual Feedback")]
    [Tooltip("Rotator component for drum spinning")]
    public PxP.Rotator rotator;
    
    [Tooltip("Light that turns on when running (e.g., LED indicator)")]
    public Light indicatorLight;
    
    [Tooltip("Particle effect when running (e.g., vibration particles)")]
    public ParticleSystem vibrationParticles;
    
    [Header("UI")]
    [Tooltip("Reference to WashingMachineUI component")]
    public WashingMachineUI washingMachineUI;
    
    [Tooltip("Timer display text (legacy, use WashingMachineUI instead)")]
    public TMPro.TMP_Text timerText;
    
    // Private variables
    private bool playerNear = false;
    private bool timerActive = false;
    private float currentTimer = 0f;
    private float soundEmitTimer = 0f;
    private Coroutine runningCoroutine = null;
    
    void Start()
    {
        // Setup audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 25f;
        audioSource.loop = false;
        
        // Auto-detect room
        if (roomID == 0)
        {
            DetectRoom();
        }
        
        // Initial state
        SetMachineState(false);
        
        // Disable rotator initially
        if (rotator != null)
            rotator.enabled = false;
    }
    
    void DetectRoom()
    {
        // Try to find room from overlapping triggers
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.5f);
        foreach (Collider col in colliders)
        {
            RoomTrigger room = col.GetComponent<RoomTrigger>();
            if (room != null)
            {
                roomID = room.roomID;
                Debug.Log($"[WashingMachine] Auto-detected Room {roomID}");
                return;
            }
        }
        
        // Raycast down
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 5f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null)
            {
                roomID = room.roomID;
                Debug.Log($"[WashingMachine] Auto-detected Room {roomID} via raycast");
            }
        }
    }
    
    void Update()
    {
        // Handle timer countdown
        if (timerActive && !isRunning)
        {
            currentTimer -= Time.deltaTime;
            
            // Update legacy UI (if using old system without WashingMachineUI)
            if (timerText != null)
            {
                timerText.text = $"Starting in: {Mathf.CeilToInt(currentTimer)}s";
            }
            
            // Timer finished - start machine!
            if (currentTimer <= 0f)
            {
                timerActive = false;
                StartMachine();
            }
        }
        
        // Handle sound emission while running
        if (isRunning)
        {
            soundEmitTimer -= Time.deltaTime;
            
            if (soundEmitTimer <= 0f)
            {
                EmitSound();
                soundEmitTimer = soundEmitInterval;
            }
        }
    }
    
    /// <summary>
    /// Set a timer to start the washing machine
    /// </summary>
    public void SetTimer(float delay)
    {
        if (isRunning) return;
        
        delay = Mathf.Clamp(delay, minTimerDelay, maxTimerDelay);
        currentTimer = delay;
        timerActive = true;
        
        Debug.Log($"[WashingMachine] Timer set: {delay} seconds");
        
        // Play timer set sound
        if (timerSetSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(timerSetSound, volume);
        }
        
        // Update UI
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = $"Starting in: {Mathf.CeilToInt(currentTimer)}s";
        }
    }
    
    /// <summary>
    /// Cancel the timer
    /// </summary>
    public void CancelTimer()
    {
        if (!timerActive) return;
        
        timerActive = false;
        currentTimer = 0f;
        
        Debug.Log("[WashingMachine] Timer cancelled");
        
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }
    }
    
    // ==================== MACHINE CONTROL ====================
    
    /// <summary>
    /// Start the washing machine immediately
    /// </summary>
    public void StartMachine()
    {
        if (isRunning) return;
        
        Debug.Log("[WashingMachine] STARTING!");
        
        isRunning = true;
        SetMachineState(true);
        
        // Play start sound
        if (startSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(startSound, volume);
        }
        
        // Start looping sound
        if (runningLoopSound != null && audioSource != null)
        {
            audioSource.clip = runningLoopSound;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        // Emit initial loud sound to attract NPCs
        EmitSound();
        soundEmitTimer = soundEmitInterval;
        
        // Hide timer text
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }
        
        // Auto-stop after duration
        if (runningCoroutine != null)
            StopCoroutine(runningCoroutine);
        runningCoroutine = StartCoroutine(AutoStopAfterDuration());
    }
    
    /// <summary>
    /// Stop the washing machine
    /// </summary>
    public void StopMachine()
    {
        if (!isRunning) return;
        
        Debug.Log("[WashingMachine] STOPPING!");
        
        isRunning = false;
        SetMachineState(false);
        
        // Stop looping sound
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
        
        // Play stop sound
        if (stopSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(stopSound, volume);
        }
    }
    
    IEnumerator AutoStopAfterDuration()
    {
        yield return new WaitForSeconds(runDuration);
        StopMachine();
    }
    
    void SetMachineState(bool running)
    {
        // Enable/disable rotator (drum spinning)
        if (rotator != null)
        {
            rotator.enabled = running;
        }
        
        // Enable/disable indicator light
        if (indicatorLight != null)
        {
            indicatorLight.enabled = running;
        }
        
        // Enable/disable particles
        if (vibrationParticles != null)
        {
            if (running)
                vibrationParticles.Play();
            else
                vibrationParticles.Stop();
        }
    }
    
    // ==================== SOUND EMISSION ====================
    
    /// <summary>
    /// Emit sound to attract NPCs
    /// ✅ FIXED: Calls OnSoundHeardWithRoom directly for CikguNPC
    /// </summary>
    void EmitSound()
    {
        if (SoundDetectionSystem.Instance == null)
        {
            Debug.LogWarning("[WashingMachine] SoundDetectionSystem not found!");
            return;
        }
        
        Debug.Log($"[WashingMachine] Emitting {soundType} sound at Room {roomID}");
        
        // ✅ NEW: Notify CikguNPC directly with room information
        // Find all CikguNPC instances and notify them
        CikguNPC[] cikgus = FindObjectsOfType<CikguNPC>();
        foreach (CikguNPC cikgu in cikgus)
        {
            // Call OnSoundHeardWithRoom which CikguNPC actually processes
            float distance = Vector3.Distance(transform.position, cikgu.GetPosition());
            cikgu.OnSoundHeardWithRoom(transform.position, soundType, distance, roomID, gameObject);
            Debug.Log($"[WashingMachine] Notified CikguNPC at distance {distance}m");
        }
        
        // ✅ ALSO: Emit regular sound for StudentNPCs (they use the old system)
        SoundDetectionSystem.Instance.EmitSound(
            transform.position,
            soundType,
            gameObject
        );
    }
    
    // ==================== TRIGGER DETECTION ====================
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Check if local player
            Photon.Pun.PhotonView pv = other.GetComponent<Photon.Pun.PhotonView>();
            if (pv != null && !pv.IsMine) return;
            
            playerNear = true;
            Debug.Log("[WashingMachine] Player nearby");
            
            // Notify UI
            if (washingMachineUI != null)
            {
                washingMachineUI.OnPlayerEnter();
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Photon.Pun.PhotonView pv = other.GetComponent<Photon.Pun.PhotonView>();
            if (pv != null && !pv.IsMine) return;
            
            playerNear = false;
            
            // Notify UI
            if (washingMachineUI != null)
            {
                washingMachineUI.OnPlayerExit();
            }
        }
    }
    
    // ==================== DEBUG ====================
    
    void OnDrawGizmosSelected()
    {
        // Draw trigger range (use collider bounds)
        Gizmos.color = Color.yellow;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        
        // Draw sound range when running
        if (isRunning || Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            float soundRange = 20f; // Loud sound range
            Gizmos.DrawWireSphere(transform.position, soundRange);
        }
    }
}