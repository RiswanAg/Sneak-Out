using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SecurityGuardNPC.cs - Security Guard AI for Level 3
/// 
/// BEHAVIOR:
/// - Phase 1 (Start): Patrols guard post area, vision cone active, CAN'T hear sounds
/// - Phase 2 (After success cutscene): Goes to control room, patrols there, CAN hear sounds (10m)
/// - Chase: When player spotted, chases until caught or lost
/// - Caught: Triggers caught cutscene and game over
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SecurityGuardNPC : MonoBehaviourPun, ISoundListener
{
    [Header("Guard State")]
    [Tooltip("Current behavior mode")]
    public GuardMode currentMode = GuardMode.PatrolGuardPost;

    [Header("Patrol Points")]
    [Tooltip("Initial patrol points around guard post")]
    public Transform[] guardPostPatrolPoints;
    
    [Tooltip("Patrol points around control room (after cutscene)")]
    public Transform[] controlRoomPatrolPoints;

    [Header("Detection Settings")]
    [Tooltip("Vision cone range in meters")]
    public float visionRange = 10f;
    
    [Tooltip("Vision cone angle (180° = half circle)")]
    public float visionAngle = 180f;
    
    [Tooltip("How long player must be in sight to be detected (seconds)")]
    public float detectionTime = 1.5f;
    
    [Tooltip("Sound detection range (only active after cutscene)")]
    public float soundDetectionRange = 10f;

    [Header("Movement")]
    [Tooltip("Patrol walking speed")]
    public float patrolSpeed = 2f;
    
    [Tooltip("Chase running speed")]
    public float chaseSpeed = 4f;
    
    [Tooltip("How long to wait at each patrol point")]
    public float waitTimeAtPoint = 2f;
    
    [Tooltip("Distance to reach player to catch them")]
    public float catchDistance = 1.5f;

    [Header("Investigation")]
    [Tooltip("Time spent investigating a sound location")]
    public float investigationTime = 5f;
    
    [Tooltip("Time spent looking around before resuming patrol")]
    public float lookAroundTime = 3f;

    [Header("References")]
    public Animator animator;
    public Transform visionOrigin; // Eye level position for vision checks
    
    [Header("Debug")]
    public bool showVisionCone = true;
    public Color visionConeColor = new Color(1f, 0f, 0f, 0.3f);

    // Components
    private NavMeshAgent agent;
    
    // State
    private GuardMode previousMode;
    private int currentPatrolIndex = 0;
    private Transform[] currentPatrolPoints;
    private Vector3 lastKnownPlayerPosition;
    private GameObject targetPlayer;
    private float detectionProgress = 0f;
    private bool isInvestigating = false;
    private bool canHearSounds = false; // Only true after success cutscene
    
    // Animation hashes
    private int animIsWalking;
    private int animIsRunning;
    private int animIsInvestigating;

    public enum GuardMode
    {
        PatrolGuardPost,    // Initial patrol around guard post
        GoingToControlRoom, // Walking to control room after cutscene
        PatrolControlRoom,  // Patrolling control room area
        Investigating,      // Checking a sound location
        Chasing,            // Chasing spotted player
        Catching            // Caught the player
    }

    void Start()
    {
        // Get components
        agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponent<Animator>();

        // Set vision origin to head/eye level if not assigned
        if (visionOrigin == null)
        {
            GameObject eyePos = new GameObject("VisionOrigin");
            eyePos.transform.SetParent(transform);
            eyePos.transform.localPosition = new Vector3(0, 1.7f, 0); // Eye level
            visionOrigin = eyePos.transform;
        }

        // Cache animation parameters (check if they exist first)
        if (animator != null)
        {
            animIsWalking = Animator.StringToHash("IsWalking");
            animIsRunning = Animator.StringToHash("IsRunning");
            animIsInvestigating = Animator.StringToHash("IsInvestigating");
        }

        // Setup agent
        if (agent != null)
        {
            agent.speed = patrolSpeed;
            agent.stoppingDistance = 0.5f;
        }

        // Start initial patrol
        currentPatrolPoints = guardPostPatrolPoints;
        StartCoroutine(PatrolRoutine());

        Debug.Log($"<color=cyan>[Guard] Security Guard initialized - Mode: {currentMode}</color>");
    }

    void Update()
    {
        // Only Master Client controls the guard
        if (!PhotonNetwork.IsMasterClient) return;

        if (currentMode == GuardMode.Chasing)
        {
            ChasePlayer();
        }
        else if (currentMode == GuardMode.PatrolGuardPost || currentMode == GuardMode.PatrolControlRoom)
        {
            CheckForPlayers();
        }
    }

    // ==================== PATROL SYSTEM ====================

    IEnumerator PatrolRoutine()
    {
        while (currentMode == GuardMode.PatrolGuardPost || currentMode == GuardMode.PatrolControlRoom)
        {
            // Make sure we have patrol points
            if (currentPatrolPoints == null || currentPatrolPoints.Length == 0)
            {
                Debug.LogWarning("[Guard] No patrol points assigned!");
                yield return new WaitForSeconds(2f);
                continue;
            }

            // Get next patrol point
            Transform targetPoint = currentPatrolPoints[currentPatrolIndex];
            
            if (targetPoint == null)
            {
                Debug.LogWarning($"[Guard] Patrol point {currentPatrolIndex} is null!");
                currentPatrolIndex = (currentPatrolIndex + 1) % currentPatrolPoints.Length;
                continue;
            }

            // Walk to patrol point
            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = patrolSpeed;
                agent.SetDestination(targetPoint.position);
                UpdateAnimation(true, false, false);

                // Wait until reached
                while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                {
                    // Check if mode changed
                    if (currentMode != GuardMode.PatrolGuardPost && currentMode != GuardMode.PatrolControlRoom)
                    {
                        yield break;
                    }
                    yield return null;
                }

                // Reached point - wait here
                UpdateAnimation(false, false, false);
                yield return new WaitForSeconds(waitTimeAtPoint);

                // Move to next point
                currentPatrolIndex = (currentPatrolIndex + 1) % currentPatrolPoints.Length;
            }
            else
            {
                Debug.LogError("[Guard] NavMeshAgent is not on NavMesh!");
                yield return new WaitForSeconds(1f);
            }

            yield return null;
        }
    }

    // ==================== VISION DETECTION ====================

    void CheckForPlayers()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            if (CanSeePlayer(player))
            {
                // Player is in vision cone
                detectionProgress += Time.deltaTime;

                if (detectionProgress >= detectionTime)
                {
                    // Player detected!
                    OnPlayerDetected(player);
                    return;
                }
            }
        }

        // No player in sight - reset detection
        detectionProgress = Mathf.Max(0, detectionProgress - Time.deltaTime * 0.5f);
    }

    bool CanSeePlayer(GameObject player)
    {
        if (player == null || visionOrigin == null) return false;

        Vector3 guardPos = visionOrigin.position;
        Vector3 playerPos = player.transform.position + Vector3.up * 1f; // Player center

        // Distance check
        float distance = Vector3.Distance(guardPos, playerPos);
        if (distance > visionRange)
            return false;

        // Angle check
        Vector3 directionToPlayer = (playerPos - guardPos).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        
        if (angle > visionAngle / 2f)
            return false;

        // Line of sight check
        RaycastHit hit;
        if (Physics.Raycast(guardPos, directionToPlayer, out hit, visionRange))
        {
            if (hit.collider.gameObject == player || hit.collider.transform.IsChildOf(player.transform))
            {
                return true; // Can see player
            }
        }

        return false;
    }

    void OnPlayerDetected(GameObject player)
    {
        Debug.Log($"<color=red>[Guard] PLAYER SPOTTED! Starting chase!</color>");

        targetPlayer = player;
        lastKnownPlayerPosition = player.transform.position;
        
        // Switch to chase mode
        previousMode = currentMode;
        currentMode = GuardMode.Chasing;
        
        detectionProgress = 0f;
    }

    // ==================== CHASE SYSTEM ====================

    void ChasePlayer()
    {
        if (targetPlayer == null)
        {
            // Lost target
            Debug.Log("[Guard] Lost target, investigating last position...");
            currentMode = GuardMode.Investigating;
            StartCoroutine(InvestigateLocation(lastKnownPlayerPosition));
            return;
        }

        // Update last known position
        lastKnownPlayerPosition = targetPlayer.transform.position;

        // Chase player
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(lastKnownPlayerPosition);
            UpdateAnimation(false, true, false);

            // Check if caught player
            float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (distance <= catchDistance)
            {
                CatchPlayer();
            }

            // Check if still in sight
            if (!CanSeePlayer(targetPlayer))
            {
                // Player escaped vision
                targetPlayer = null;
            }
        }
    }

    void CatchPlayer()
    {
        Debug.Log("<color=red>[Guard] PLAYER CAUGHT!</color>");

        currentMode = GuardMode.Catching;
        UpdateAnimation(false, false, false);

        // Stop movement
        if (agent != null)
            agent.isStopped = true;

        // Trigger caught cutscene (only Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            MidLevelCutsceneManager cutsceneManager = FindObjectOfType<MidLevelCutsceneManager>();
            if (cutsceneManager != null)
            {
                cutsceneManager.PlayCaughtCutscene();
            }
            else
            {
                Debug.LogError("MidLevelCutsceneManager not found!");
            }
        }
    }

    // ==================== SOUND DETECTION ====================

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void OnSoundHeard(Vector3 soundPosition, SoundType soundType, float distance, GameObject source)
    {
        // Only react to sounds after success cutscene
        if (!canHearSounds) return;

        // Ignore if already chasing or catching
        if (currentMode == GuardMode.Chasing || currentMode == GuardMode.Catching)
            return;

        // Distance check
        if (distance > soundDetectionRange)
            return;

        // Ignore very quiet sounds
        if (soundType == SoundType.VeryQuiet || soundType == SoundType.Silent)
            return;

        Debug.Log($"<color=yellow>[Guard] Heard sound at distance {distance:F1}m - Investigating!</color>");

        // Investigate sound
        if (!isInvestigating)
        {
            previousMode = currentMode;
            currentMode = GuardMode.Investigating;
            StartCoroutine(InvestigateLocation(soundPosition));
        }
    }

    IEnumerator InvestigateLocation(Vector3 location)
    {
        isInvestigating = true;

        Debug.Log($"[Guard] Investigating location: {location}");

        // Walk to sound location
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(location);
            UpdateAnimation(true, false, false);

            // Wait until reached
            float timeout = 10f;
            float timer = 0f;
            
            while ((agent.pathPending || agent.remainingDistance > agent.stoppingDistance) && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Look around
            UpdateAnimation(false, false, true);
            yield return new WaitForSeconds(investigationTime);

            // Resume patrol
            Debug.Log("[Guard] Investigation complete, resuming patrol");
            currentMode = previousMode;
            UpdateAnimation(false, false, false);
        }

        isInvestigating = false;

        // Restart patrol
        if (currentMode == GuardMode.PatrolGuardPost || currentMode == GuardMode.PatrolControlRoom)
        {
            StartCoroutine(PatrolRoutine());
        }
    }

    // ==================== MODE CHANGES ====================

    /// <summary>
    /// Called by MidLevelCutsceneManager after success cutscene
    /// Guard goes to control room and starts patrolling there
    /// </summary>
    public void ActivatePatrolMode()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("<color=green>[Guard] Activating patrol mode - Going to control room!</color>");

        // Enable sound detection
        canHearSounds = true;

        // Register with sound system
        if (SoundDetectionSystem.Instance != null)
        {
            SoundDetectionSystem.Instance.RegisterListener(this);
        }

        // Change mode
        currentMode = GuardMode.GoingToControlRoom;
        
        // Go to control room
        StartCoroutine(GoToControlRoom());
    }

    IEnumerator GoToControlRoom()
    {
        Debug.Log("[Guard] Walking to control room...");

        // Make sure we have control room patrol points
        if (controlRoomPatrolPoints == null || controlRoomPatrolPoints.Length == 0)
        {
            Debug.LogError("[Guard] No control room patrol points assigned!");
            yield break;
        }

        // Walk to first control room patrol point
        Transform controlRoomPoint = controlRoomPatrolPoints[0];
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(controlRoomPoint.position);
            UpdateAnimation(true, false, false);

            // Wait until reached
            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            {
                yield return null;
            }

            Debug.Log("[Guard] Reached control room - Starting patrol!");

            // Switch to control room patrol
            currentMode = GuardMode.PatrolControlRoom;
            currentPatrolPoints = controlRoomPatrolPoints;
            currentPatrolIndex = 0;

            // Start patrol routine
            StartCoroutine(PatrolRoutine());
        }
    }

    // ==================== ANIMATION ====================

    void UpdateAnimation(bool walking, bool running, bool investigating)
    {
        if (animator == null) return;

        // Check if parameters exist before setting them
        try
        {
            animator.SetBool(animIsWalking, walking);
            animator.SetBool(animIsRunning, running);
            animator.SetBool(animIsInvestigating, investigating);
        }
        catch
        {
            // Animation parameters don't exist yet - that's okay
        }
    }

    // ==================== DEBUG VISUALIZATION ====================

    void OnDrawGizmosSelected()
    {
        if (!showVisionCone) return;

        Vector3 startPos = visionOrigin != null ? visionOrigin.position : transform.position + Vector3.up * 1.7f;

        // Draw vision cone
        Gizmos.color = visionConeColor;
        
        Vector3 forward = transform.forward * visionRange;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle / 2f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, visionAngle / 2f, 0) * forward;

        // Draw cone lines
        Gizmos.DrawLine(startPos, startPos + leftBoundary);
        Gizmos.DrawLine(startPos, startPos + rightBoundary);
        
        // Draw arc
        int segments = 20;
        Vector3 previousPoint = startPos + leftBoundary;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = -visionAngle / 2f + (visionAngle / segments) * i;
            Vector3 point = startPos + Quaternion.Euler(0, angle, 0) * forward;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }

        // Draw patrol points
        if (currentPatrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform point in currentPatrolPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                }
            }
        }

        // Draw chase target
        if (currentMode == GuardMode.Chasing && targetPlayer != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetPlayer.transform.position);
        }
    }

    void OnDestroy()
    {
        // Unregister from sound system
        if (SoundDetectionSystem.Instance != null)
        {
            SoundDetectionSystem.Instance.UnregisterListener(this);
        }
    }
}