using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Photon.Pun;

public enum StudentState
{
    Sleeping,
    Idle,
    Sitting,
    AlertLooking,      // Looking around after hearing sound
    WalkingToSound,    // Walking to investigate
    InvestigatingSound, // At sound location, looking around
    ReturningHome,     // Walking back
    ReturnedLooking    // Looking around before sitting back down
}

[RequireComponent(typeof(NavMeshAgent))]
public class StudentNPC : MonoBehaviour, ISoundListener
{
    [Header("Student Type")]
    [Tooltip("Is this student initially sleeping?")]
    public bool isSleeping = false;
    
    [Tooltip("Initial state for awake students")]
    public StudentState initialState = StudentState.Idle;
    
    [Header("Sleeping Student Settings")]
    [Tooltip("Sleeping student only wakes up, doesn't investigate")]
    public bool onlyWakeUp = false;
    
    [Tooltip("If true, waking this sleeping student causes GAME OVER (Level 1)")]
    public bool gameOverOnWake = true;
    
    [Header("Vision Detection (Awake Students)")]
    [Tooltip("Enable vision detection for awake students")]
    public bool hasVision = true;
    
    [Tooltip("Detection range for seeing players")]
    public float visionRange = 10f;
    
    [Tooltip("Field of view angle (degrees)")]
    public float visionAngle = 90f;
    
    [Tooltip("How often to check for players (seconds)")]
    public float visionCheckInterval = 0.2f;
    
    [Tooltip("Layer mask for players")]
    public LayerMask playerLayer;
    
    [Tooltip("Layer mask for obstacles blocking vision")]
    public LayerMask obstacleLayer;
    
    [Tooltip("Show vision cone in Scene view")]
    public bool showVisionDebug = true;
    
    [Header("Investigation Settings")]
    [Tooltip("Time spent looking around before moving (seconds)")]
    public float alertLookTime = 2f;
    
    [Tooltip("Time spent investigating at sound location (seconds)")]
    public float investigationTime = 5f;
    
    [Tooltip("Time spent looking around after returning (seconds)")]
    public float returnedLookTime = 2f;
    
    [Tooltip("Walking speed while investigating")]
    public float walkSpeed = 2f;
    
    [Header("Alert Indicator")]
    public GameObject alertIndicator;
    public float alertIndicatorHeight = 2f;
    
    [Header("References")]
    public Animator animator;
    
    [Header("Room Detection")]
    [Tooltip("Room ID this student is in (0 = hears all rooms)")]
    public int myRoomID;
    
    [Header("Audio/Voice Lines")]
    [Tooltip("AudioSource for voice lines")]
    public AudioSource audioSource;
    
    [Tooltip("Voice lines when alerted (random selection)")]
    public AudioClip[] alertVoiceLines;
    
    [Tooltip("Voice lines when investigating")]
    public AudioClip[] investigatingVoiceLines;
    
    [Tooltip("Voice lines when returning home")]
    public AudioClip[] returnVoiceLines;
    
    [Tooltip("Voice line when sleeping student wakes up")]
    public AudioClip wakeUpVoiceLine;
    
    [Tooltip("Voice lines when spotting player - plays RIGHT BEFORE game over")]
    public AudioClip[] spottedPlayerVoiceLines;
    
    [Tooltip("Volume for voice lines")]
    [Range(0f, 1f)]
    public float voiceVolume = 1f;
    
    [Tooltip("Cooldown between voice lines (seconds)")]
    public float voiceCooldown = 3f;
    
    // Private variables
    private NavMeshAgent agent;
    private StudentState currentState;
    private StudentState stateBeforeInvestigation;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 soundLocation;
    private bool isInvestigating = false;
    private float lastVoiceTime = -999f;
    private float visionCheckTimer;
    
    // Animation parameter hashes
    private int animIsWalking;
    private int animIsSitting;
    private int animIsSleeping;
    private int animIsInvestigating;
    private int animWakeUp;
    
    // Static flag to prevent multiple game overs
    private static bool hasTriggeredGameOver = false;
    
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        
        // Setup audio source if not assigned
        if (audioSource == null && (alertVoiceLines.Length > 0 || wakeUpVoiceLine != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 15f;
        }
        
        // Cache animation parameters
        animIsWalking = Animator.StringToHash("IsWalking");
        animIsSitting = Animator.StringToHash("IsSitting");
        animIsSleeping = Animator.StringToHash("IsSleeping");
        animIsInvestigating = Animator.StringToHash("IsInvestigating");
        animWakeUp = Animator.StringToHash("WakeUp");
        
        // Save home position and rotation
        homePosition = transform.position;
        homeRotation = transform.rotation;
        
        // Set agent speed
        if (agent != null)
            agent.speed = walkSpeed;
        
        // Set initial state
        currentState = isSleeping ? StudentState.Sleeping : initialState;
        UpdateAnimator();
        
        // Setup alert indicator
        if (alertIndicator != null)
        {
            alertIndicator.SetActive(false);
            alertIndicator.transform.SetParent(transform);
            alertIndicator.transform.localPosition = Vector3.up * alertIndicatorHeight;
        }
        
        // Auto-detect room if myRoomID is 0
        if (myRoomID == 0)
        {
            DetectMyRoom();
        }
        
        // Register with sound system
        if (SoundDetectionSystem.Instance != null)
        {
            SoundDetectionSystem.Instance.RegisterListener(this);
        }
        
        // Reset static flag on scene load
        hasTriggeredGameOver = false;
        
        // Set player layer if not set
        if (playerLayer.value == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
        }
        
        Debug.Log($"{gameObject.name} initialized in Room {myRoomID}, state: {currentState}, hasVision: {hasVision}");
    }
    
    void DetectMyRoom()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null)
            {
                myRoomID = room.roomID;
                Debug.Log($"{gameObject.name} auto-detected Room {myRoomID}");
            }
        }
    }
    
    void OnDestroy()
    {
        if (SoundDetectionSystem.Instance != null)
        {
            SoundDetectionSystem.Instance.UnregisterListener(this);
        }
    }
    
    void Update()
    {
        UpdateAlertIndicator();
        
        // ✅ VISION CHECK - Works in ALL states except sleeping
        if (hasVision && currentState != StudentState.Sleeping && !hasTriggeredGameOver)
        {
            visionCheckTimer -= Time.deltaTime;
            if (visionCheckTimer <= 0f)
            {
                visionCheckTimer = visionCheckInterval;
                CheckForPlayers();
            }
        }
    }
    
    // ==================== VISION DETECTION ====================
    
    /// <summary>
    /// ✅ Check for players in vision cone - INSTANT GAME OVER if spotted
    /// Works during: Sitting, Idle, Alert, Walking, Investigating, Returning
    /// </summary>
    void CheckForPlayers()
    {
        // Don't check if already triggered game over
        if (hasTriggeredGameOver) return;
        
        // Find all players in range
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, visionRange, playerLayer);
        
        foreach (Collider playerCollider in playersInRange)
        {
            // Skip if not a player
            if (!playerCollider.CompareTag("Player")) continue;
            
            // Only detect players with PhotonView
            PhotonView playerPV = playerCollider.GetComponent<PhotonView>();
            if (playerPV == null) continue;
            
            // Get player position (use center of character, not feet)
            Vector3 playerPosition = playerCollider.transform.position + Vector3.up * 1f;
            Vector3 npcEyePosition = transform.position + Vector3.up * 1.5f;
            
            // Calculate direction to player
            Vector3 directionToPlayer = (playerPosition - npcEyePosition).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            
            // Check if player is within field of view
            if (angleToPlayer <= visionAngle / 2f)
            {
                // Check if line of sight is clear (no obstacles)
                RaycastHit hit;
                float distanceToPlayer = Vector3.Distance(npcEyePosition, playerPosition);
                
                // Raycast from NPC eyes to player center
                if (Physics.Raycast(npcEyePosition, directionToPlayer, out hit, distanceToPlayer, obstacleLayer))
                {
                    // Something is blocking view
                    Debug.Log($"{gameObject.name} - Vision blocked by {hit.collider.name}");
                    continue;
                }
                
                // ✅ PLAYER SPOTTED - INSTANT GAME OVER!
                Debug.Log($"[StudentNPC] {gameObject.name} SPOTTED PLAYER: {playerCollider.name}! GAME OVER!");
                PlayerSpotted(playerCollider.gameObject);
                return;
            }
        }
    }
    
    /// <summary>
    /// ✅ Player spotted - INSTANT GAME OVER (no chase)
    /// </summary>
    void PlayerSpotted(GameObject player)
    {
        if (hasTriggeredGameOver) return;
        hasTriggeredGameOver = true;
        
        Debug.Log($"[StudentNPC] {gameObject.name} caught {player.name} by sight!");
        
        // ✅ STOP ALL INVESTIGATION IMMEDIATELY
        if (isInvestigating)
        {
            StopAllCoroutines(); // Stop investigation sequence
            isInvestigating = false;
        }
        
        // ✅ STOP MOVEMENT
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        
        // ✅ FREEZE IN IDLE ANIMATION
        currentState = StudentState.Idle;
        UpdateAnimator();
        
        // Show alert indicator
        if (alertIndicator != null)
            alertIndicator.SetActive(true);
        
        // Play spotted voice line (optional)
        PlayRandomVoiceLine(spottedPlayerVoiceLines);
        
        // Trigger game over for the caught player
        PhotonView playerPV = player.GetComponent<PhotonView>();
        if (playerPV != null && playerPV.IsMine)
        {
            // This is the local player - trigger game over
            if (Level1Manager.Instance != null)
            {
                Level1Manager.Instance.PlayerFailed(player);
            }
            else
            {
                Debug.LogError("[StudentNPC] Level1Manager not found!");
            }
        }
    }
    
    // ==================== SOUND DETECTION ====================
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    /// <summary>
    /// Old method - uses player's room (for footsteps, backward compatibility)
    /// </summary>
    public void OnSoundHeard(Vector3 soundPosition, SoundType soundType, float distance, GameObject source)
    {
        int soundRoomID = PlayerRoomTracker.CurrentRoomID;
        ProcessSound(soundPosition, soundType, distance, soundRoomID, source);
    }
    
    /// <summary>
    /// ✅ NEW: Method with explicit room ID (for thrown items)
    /// </summary>
    public void OnSoundHeardWithRoom(Vector3 soundPosition, SoundType soundType, float distance, int soundRoomID, GameObject source)
    {
        ProcessSound(soundPosition, soundType, distance, soundRoomID, source);
    }
    
    /// <summary>
    /// Process the sound - shared logic
    /// </summary>
    private void ProcessSound(Vector3 soundPosition, SoundType soundType, float distance, int soundRoomID, GameObject source)
    {
        // Ignore own sounds
        if (source == gameObject) return;
        
        // Don't process sounds if already caught someone
        if (hasTriggeredGameOver) return;
        
        Debug.Log($"{gameObject.name} (Room {myRoomID}) heard sound from Room {soundRoomID}, Distance: {distance}m, Type: {soundType}");
        
        // ROOM CHECK: Only hear sounds from same room
        if (myRoomID != soundRoomID && myRoomID != 0)
        {
            Debug.Log($"{gameObject.name} (Room {myRoomID}) IGNORING sound from Room {soundRoomID} - different room!");
            return;
        }
        
        Debug.Log($"{gameObject.name} is in same room! Proceeding with sound detection...");
        
        // SLEEPING STUDENT
        if (currentState == StudentState.Sleeping)
        {
            if (soundType == SoundType.Loud)
            {
                if (gameOverOnWake)
                {
                    // ✅ TRIGGER GAME OVER - Sleeping student woke up!
                    Debug.Log($"[StudentNPC] {gameObject.name} - GAME OVER! Sleeping student woke up!");
                    StartCoroutine(WakeUpAndTriggerGameOver(source));
                }
                else if (onlyWakeUp)
                {
                    Debug.Log($"{gameObject.name} - Sleeping student waking up (no investigation)");
                    StartCoroutine(WakeUpAndSleep());
                }
                else
                {
                    Debug.Log($"{gameObject.name} - Sleeping student waking up and investigating");
                    currentState = StudentState.Idle;
                    if (animator != null)
                        animator.SetTrigger(animWakeUp);
                    StartInvestigation(soundPosition);
                }
            }
            else
            {
                Debug.Log($"{gameObject.name} - Sound not loud enough to wake sleeping student");
            }
            return;
        }
        
        // AWAKE STUDENT - Ignore if already investigating
        if (isInvestigating)
        {
            Debug.Log($"{gameObject.name} is already investigating, ignoring new sound");
            return;
        }
        
        // Only investigate if not too quiet and far
        if (soundType == SoundType.VeryQuiet && distance > 3f)
        {
            Debug.Log($"{gameObject.name} - Sound too quiet and far, ignoring");
            return;
        }
        
        // Start investigation!
        Debug.Log($"{gameObject.name} - Starting investigation!");
        StartInvestigation(soundPosition);
    }
    
    // ==================== INVESTIGATION FLOW ====================
    
    private void StartInvestigation(Vector3 soundPos)
    {
        if (isInvestigating) return;
        
        isInvestigating = true;
        soundLocation = soundPos;
        stateBeforeInvestigation = currentState;
        
        Debug.Log($"{gameObject.name} heard sound at {soundPos}, starting investigation from {stateBeforeInvestigation}");
        
        // Show alert
        if (alertIndicator != null)
            alertIndicator.SetActive(true);
        
        // Play alert voice line
        PlayRandomVoiceLine(alertVoiceLines);
        
        // Start investigation sequence
        StartCoroutine(InvestigationSequence());
    }
    
    private IEnumerator InvestigationSequence()
    {
        // STEP 1: Look around while in place
        Debug.Log($"{gameObject.name} - Step 1: Looking around");
        currentState = StudentState.AlertLooking;
        UpdateAnimator();
        yield return new WaitForSeconds(alertLookTime);
        
        // STEP 2: Stand up if sitting
        if (stateBeforeInvestigation == StudentState.Sitting)
        {
            Debug.Log($"{gameObject.name} - Step 2: Standing up from sitting");
            currentState = StudentState.Idle;
            UpdateAnimator();
            yield return new WaitForSeconds(0.5f);
        }
        
        // STEP 3: Walk to sound location
        Debug.Log($"{gameObject.name} - Step 3: Walking to sound");
        currentState = StudentState.WalkingToSound;
        UpdateAnimator();
        
        // Play investigating voice line
        PlayRandomVoiceLine(investigatingVoiceLines);
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(soundLocation);
            
            float walkTimer = 0f;
            float maxWalkTime = 10f;
            
            while (agent.pathPending || (agent.remainingDistance > agent.stoppingDistance && walkTimer < maxWalkTime))
            {
                walkTimer += Time.deltaTime;
                
                // ✅ Vision still active while walking - can spot player anytime
                yield return null;
            }
            
            if (walkTimer >= maxWalkTime)
            {
                Debug.LogWarning($"{gameObject.name} couldn't reach sound location, canceling investigation");
                CancelInvestigation();
                yield break;
            }
        }
        else
        {
            Debug.LogError($"{gameObject.name} NavMeshAgent not valid! Canceling investigation");
            CancelInvestigation();
            yield break;
        }
        
        // STEP 4: Investigate at location
        Debug.Log($"{gameObject.name} - Step 4: Investigating at location");
        currentState = StudentState.InvestigatingSound;
        UpdateAnimator();
        
        // ✅ Vision still active while investigating
        yield return new WaitForSeconds(investigationTime);
        
        // STEP 5: Walk back home
        Debug.Log($"{gameObject.name} - Step 5: Returning home");
        currentState = StudentState.ReturningHome;
        UpdateAnimator();
        
        // Play return voice line
        PlayRandomVoiceLine(returnVoiceLines);
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(homePosition);
            
            float returnTimer = 0f;
            float maxReturnTime = 10f;
            
            while (agent.pathPending || (agent.remainingDistance > agent.stoppingDistance && returnTimer < maxReturnTime))
            {
                returnTimer += Time.deltaTime;
                
                // ✅ Vision still active while returning - can spot player anytime
                yield return null;
            }
        }
        
        // Restore original rotation
        transform.rotation = homeRotation;
        
        // STEP 6: Look around at home position
        Debug.Log($"{gameObject.name} - Step 6: Looking around after returning");
        currentState = StudentState.ReturnedLooking;
        UpdateAnimator();
        
        // ✅ Vision still active while looking around
        yield return new WaitForSeconds(returnedLookTime);
        
        // STEP 7: Return to original state
        Debug.Log($"{gameObject.name} - Step 7: Returning to original state: {stateBeforeInvestigation}");
        currentState = stateBeforeInvestigation;
        UpdateAnimator();
        
        // Hide alert
        if (alertIndicator != null)
            alertIndicator.SetActive(false);
        
        // Investigation complete
        isInvestigating = false;
        Debug.Log($"{gameObject.name} investigation complete!");
    }
    
    private void CancelInvestigation()
    {
        currentState = stateBeforeInvestigation;
        UpdateAnimator();
        
        if (alertIndicator != null)
            alertIndicator.SetActive(false);
        
        isInvestigating = false;
        Debug.Log($"{gameObject.name} investigation CANCELED");
    }
    
    // ==================== SLEEPING STUDENT GAME OVER ====================
    
    /// <summary>
    /// ✅ Sleeping student wakes up and triggers GAME OVER
    /// </summary>
    private IEnumerator WakeUpAndTriggerGameOver(GameObject source)
    {
        if (hasTriggeredGameOver) yield break;
        
        Debug.Log($"[StudentNPC] {gameObject.name} waking up - GAME OVER!");
        
        // Trigger wake animation
        if (animator != null)
            animator.SetTrigger(animWakeUp);
        
        // Show alert
        if (alertIndicator != null)
            alertIndicator.SetActive(true);
        
        // Play wake up voice line
        PlayVoiceLine(wakeUpVoiceLine);
        
        // Wait a moment for dramatic effect
        yield return new WaitForSeconds(1.5f);
        
        // Trigger game over
        if (!hasTriggeredGameOver)
        {
            hasTriggeredGameOver = true;
            
            // Find the player who made the sound
            GameObject playerToFail = source;
            
            // If source is not a player, find the nearest player
            if (playerToFail == null || !playerToFail.CompareTag("Player"))
            {
                playerToFail = FindNearestPlayer();
            }
            
            if (playerToFail != null)
            {
                PhotonView playerPV = playerToFail.GetComponent<PhotonView>();
                if (playerPV != null && playerPV.IsMine)
                {
                    if (Level1Manager.Instance != null)
                    {
                        Level1Manager.Instance.PlayerFailed(playerToFail);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Sleeping student wakes up briefly then goes back to sleep (no game over)
    /// </summary>
    private IEnumerator WakeUpAndSleep()
    {
        Debug.Log($"{gameObject.name} waking up briefly");
        
        // Trigger wake animation
        if (animator != null)
            animator.SetTrigger(animWakeUp);
        
        // Show alert briefly
        if (alertIndicator != null)
            alertIndicator.SetActive(true);
        
        // Play wake up voice line
        PlayVoiceLine(wakeUpVoiceLine);
        
        // Wait for wake animation
        yield return new WaitForSeconds(3f);
        
        // Go back to sleep
        currentState = StudentState.Sleeping;
        UpdateAnimator();
        
        if (alertIndicator != null)
            alertIndicator.SetActive(false);
        
        Debug.Log($"{gameObject.name} went back to sleep");
    }
    
    GameObject FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (GameObject player in players)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = player;
            }
        }
        
        return nearest;
    }
    
    // ==================== VOICE LINES ====================
    
    void PlayRandomVoiceLine(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        if (Time.time - lastVoiceTime < voiceCooldown) return;
        
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        PlayVoiceLine(clip);
    }
    
    void PlayVoiceLine(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        if (Time.time - lastVoiceTime < voiceCooldown) return;
        
        audioSource.PlayOneShot(clip, voiceVolume);
        lastVoiceTime = Time.time;
    }
    
    // ==================== ANIMATION ====================
    
    private void UpdateAnimator()
    {
        if (animator == null)
        {
            Debug.LogWarning($"{gameObject.name} - Animator is NULL!");
            return;
        }
        
        // Reset ALL parameters first
        animator.SetBool(animIsWalking, false);
        animator.SetBool(animIsSitting, false);
        animator.SetBool(animIsSleeping, false);
        animator.SetBool(animIsInvestigating, false);
        
        // Set based on current state
        switch (currentState)
        {
            case StudentState.Sleeping:
                animator.SetBool(animIsSleeping, true);
                break;
                
            case StudentState.Sitting:
                animator.SetBool(animIsSitting, true);
                break;
                
            case StudentState.WalkingToSound:
            case StudentState.ReturningHome:
                animator.SetBool(animIsWalking, true);
                break;
                
            case StudentState.InvestigatingSound:
                animator.SetBool(animIsInvestigating, true);
                break;
                
            case StudentState.AlertLooking:
            case StudentState.ReturnedLooking:
            case StudentState.Idle:
                // All parameters false = Idle animation
                break;
        }
    }
    
    // ==================== ALERT INDICATOR ====================
    
    private void UpdateAlertIndicator()
    {
        if (alertIndicator != null && alertIndicator.activeSelf)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                alertIndicator.transform.LookAt(mainCam.transform);
                alertIndicator.transform.Rotate(0, 180, 0);
            }
        }
    }
    
    // ==================== STATIC RESET ====================
    
    /// <summary>
    /// Call this when restarting level to reset static flags
    /// </summary>
    public static void ResetStaticFlags()
    {
        hasTriggeredGameOver = false;
        Debug.Log("[StudentNPC] Static flags reset");
    }
    
    // ==================== DEBUG ====================
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 drawPos = Application.isPlaying ? homePosition : transform.position;
        Gizmos.DrawWireSphere(drawPos, 0.3f);
        
        if (isInvestigating)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(soundLocation, 0.5f);
            Gizmos.DrawLine(transform.position, soundLocation);
        }
        
        // Draw vision cone (works in all states except sleeping)
        if (showVisionDebug && hasVision && (!Application.isPlaying || currentState != StudentState.Sleeping))
        {
            Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
            
            // Draw forward ray
            Gizmos.color = Color.red;
            Vector3 forward = transform.forward * visionRange;
            Gizmos.DrawRay(eyePosition, forward);
            
            // Draw FOV cone
            Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle / 2, 0) * forward;
            Vector3 rightBoundary = Quaternion.Euler(0, visionAngle / 2, 0) * forward;
            
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawRay(eyePosition, leftBoundary);
            Gizmos.DrawRay(eyePosition, rightBoundary);
            
            // Draw arc
            int segments = 20;
            Vector3 previousPoint = eyePosition + leftBoundary;
            for (int i = 1; i <= segments; i++)
            {
                float angle = -visionAngle / 2 + (visionAngle * i / segments);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
                Vector3 point = eyePosition + direction;
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }
    }
}