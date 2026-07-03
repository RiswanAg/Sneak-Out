using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Photon.Pun;

public enum CikguState
{
    Sitting,
    WalkingToSound,
    LookingAround,      // ✅ NEW: Looking around at sound location
    Patrolling,
    Chasing,
    Yelling,            // ✅ Changed from "Angry" to match animation name
    ReturningToSeat
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PhotonView))]
public class CikguNPC : MonoBehaviourPunCallbacks, ISoundListener, IPunObservable
{
    [Header("=== CIKGU SETTINGS ===")]
    public Transform seatPosition;

    [Header("Vision Settings")]
    [Tooltip("Enable vision detection in ALL states")]
    public bool hasVision = true;
    public float visionAngle = 120f;
    public float visionRange = 100f;
    [Tooltip("How often to check for players (seconds)")]
    public float visionCheckInterval = 0.2f;
    public LayerMask visionBlockingLayers;
    public LayerMask playerLayer;

    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float chaseSpeed = 6f;

    [Header("Timing Settings")]
    public float lookAroundTime = 3f;      // ✅ Time spent looking around at sound location
    public float patrolTime = 10f;
    public float patrolRadius = 8f;

    [Header("Catch Settings")]
    [Tooltip("Distance to catch player (2 meters)")]
    public float catchDistance = 2f;
    public float stoppingDistance = 2f;

    [Header("Sound Detection")]
    public int myRoomID = 0;
    public int myFloorLevel = 0;

    [Tooltip("If enabled, Cikgu will only react to sounds included in Allowed Sound Types.")]
    public bool restrictToAllowedSoundTypes = true;

    [Tooltip("Washing machine emits SoundType.Loud, Thrown items emit Medium/Loud")]
    public SoundType[] allowedSoundTypes = new SoundType[] { SoundType.Loud, SoundType.Medium };

    [Tooltip("If enabled, ONLY react to washing machine sounds (ignore thrown items)")]
    public bool washingMachineOnly = false;

    [Tooltip("Optional: If your washing machine objects are tagged")]
    public string washingMachineTag = "WashingMachine";

    [Header("Indicators")]
    public GameObject alertIndicator;
    public GameObject exclamationIndicator;
    public float indicatorHeight = 2.2f;

    [Header("References")]
    public Animator animator;
    public AudioSource audioSource;

    [Header("Audio")]
    [Tooltip("Played ONCE when spotting player: 'Berhenti jangan bergerak!'")]
    public AudioClip spottedPlayerClip;
    
    [Tooltip("Played ONCE when catching player: 'Apa korang buat ni ha!'")]
    public AudioClip caughtPlayerClip;

    // ==================== PRIVATE ====================
    private NavMeshAgent agent;
    private CikguState currentState;
    private Vector3 seatPos;
    private Quaternion seatRot;

    // Sound tracking
    private Vector3 currentSoundTarget;
    private Vector3 pendingSound;
    private bool hasPendingSound = false;

    // Player tracking
    private GameObject targetPlayer;
    private Vector3 lastSeenPlayerPosition;
    private bool canSeePlayer = false;
    private float visionCheckTimer;

    // Timers
    private float stateTimer = 0f;
    private float patrolTimer = 0f;

    // Animation parameters (strings that match your Animator Controller)
    private const string ANIM_SITTING = "IsSitting";
    private const string ANIM_WALKING = "IsWalking";
    private const string ANIM_RUNNING = "IsRunning";
    private const string ANIM_LOOKING = "IsLooking";
    private const string ANIM_YELLING = "IsYelling";
    private const string ANIM_STAND_UP = "StandUp";
    private const string ANIM_SIT_DOWN = "SitDown";

    private Coroutine currentRoutine;

    // Network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private CikguState networkState;
    private float lerpSpeed = 10f;

    // Game over tracking
    private bool hasTriggeredGameOver = false;
    private bool hasPlayedSpottedSound = false;
    private bool hasPlayedCaughtSound = false;

    public static System.Action<GameObject> OnPlayerCaught;


    void Awake()
    {
        // ✅ Force reset all state on scene load
        hasTriggeredGameOver = false;
        hasPlayedSpottedSound = false;
        hasPlayedCaughtSound = false;
        
        currentState = CikguState.Sitting;
        targetPlayer = null;
        canSeePlayer = false;
        
        GameLog.Log("[CikguNPC] Awake - State reset");
    }

    void OnEnable()
    {
        // ✅ Reset state when enabled (handles scene reload)
        hasTriggeredGameOver = false;
        hasPlayedSpottedSound = false;
        hasPlayedCaughtSound = false;
        
        GameLog.Log("[CikguNPC] OnEnable - Flags reset");
    }
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (seatPosition != null)
        {
            seatPos = seatPosition.position;
            seatRot = seatPosition.rotation;
        }
        else
        {
            seatPos = transform.position;
            seatRot = transform.rotation;
        }

        // ✅ MASTER CLIENT: Controls AI logic
        if (PhotonNetwork.IsMasterClient)
        {
            agent.enabled = true;
            agent.speed = walkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = true;
        }
        else
        {
            // ✅ CLIENT: Disable NavMeshAgent (will lerp to network position)
            agent.enabled = false;
        }

        SetupIndicators();

        if (myRoomID == 0) DetectMyRoom();

        if (SoundDetectionSystem.Instance != null)
            SoundDetectionSystem.Instance.RegisterListener(this);

        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkState = CikguState.Sitting;

        currentState = CikguState.Sitting;
        UpdateAnimator();

        if (PhotonNetwork.IsMasterClient)
            agent.enabled = false; // Start sitting

        // Set player layer if not set
        if (playerLayer.value == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
        }

        hasTriggeredGameOver = false;
        hasPlayedSpottedSound = false;
        hasPlayedCaughtSound = false;
        
        GameLog.Log($"[CikguNPC] Initialized - Vision: {hasVision}, Room: {myRoomID}, Floor: {myFloorLevel}, IsMaster: {PhotonNetwork.IsMasterClient}");
    }

    void OnDestroy()
    {
        if (SoundDetectionSystem.Instance != null)
            SoundDetectionSystem.Instance.UnregisterListener(this);
    }

    void SetupIndicators()
    {
        if (alertIndicator != null)
        {
            alertIndicator.SetActive(false);
            alertIndicator.transform.localPosition = Vector3.up * indicatorHeight;
        }
        if (exclamationIndicator != null)
        {
            exclamationIndicator.SetActive(false);
            exclamationIndicator.transform.localPosition = Vector3.up * indicatorHeight;
        }
    }

    void DetectMyRoom()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 10f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null)
            {
                myRoomID = room.roomID;
                myFloorLevel = room.floorLevel;
            }
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsMasterClient) 
            MasterUpdate();
        else 
            ClientUpdate();

        UpdateIndicators();
    }

    void MasterUpdate()
    {
        stateTimer += Time.deltaTime;

        // ✅ VISION - Now handled by PlayerVisionDetector on each player
        // Players notify CikguNPC via RPC_PlayerSpotted when in vision
        // This allows ALL players (Master and Client) to be detected equally

//         // ✅ VISION CHECK - ACTIVE IN ALL STATES (except Yelling)
//         if (hasVision && currentState != CikguState.Yelling && !hasTriggeredGameOver)
//         {
//             visionCheckTimer -= Time.deltaTime;
//             if (visionCheckTimer <= 0f)
//             {
//                 visionCheckTimer = visionCheckInterval;
//                 CheckVision();
//             }
//         }

        // State machine
        switch (currentState)
        {
            case CikguState.Sitting:
                // Just sit and watch
                break;

            case CikguState.WalkingToSound:
                HandleWalkingToSound();
                break;

            case CikguState.LookingAround:
                HandleLookingAround();
                break;

            case CikguState.Patrolling:
                HandlePatrolling();
                break;

            case CikguState.Chasing:
                HandleChasing();
                break;

            case CikguState.Yelling:
                // Animation playing
                break;

            case CikguState.ReturningToSeat:
                HandleReturning();
                break;
        }
    }

    void ClientUpdate()
    {
        // ✅ Smoothly lerp to network position/rotation
        if (currentState != networkState)
        {
            currentState = networkState;
            UpdateAnimator();
        }

        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
    }

    // ==================== VISION SYSTEM ====================

    void CheckVision()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        bool spotDetected = false;

        foreach (GameObject player in players)
        {
            if (player == null) continue;

            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 dirToPlayer = (player.transform.position - eyePos).normalized;
            float distToPlayer = Vector3.Distance(eyePos, player.transform.position);

            if (distToPlayer > visionRange) continue;

            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > visionAngle / 2f) continue;

            // Raycast to check if vision is blocked
            RaycastHit hit;
            if (Physics.Raycast(eyePos, dirToPlayer, out hit, distToPlayer, visionBlockingLayers))
            {
                if (hit.collider.gameObject != player) continue;
            }

            // ✅ PLAYER SPOTTED!
            canSeePlayer = true;
            targetPlayer = player;
            lastSeenPlayerPosition = player.transform.position;
            spotDetected = true;

            // ✅ Play spotted sound ONCE
            if (!hasPlayedSpottedSound)
            {
                PlaySpottedSound();
                hasPlayedSpottedSound = true;
            }

            // Switch to chasing immediately
            if (currentState != CikguState.Chasing && currentState != CikguState.Yelling)
            {
                ShowExclamation();
                ChangeState(CikguState.Chasing);
            }

            break;
        }

        if (!spotDetected)
        {
            canSeePlayer = false;
        }
    }

    // ==================== STATE HANDLERS ====================

    void HandleWalkingToSound()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // Check if arrived at sound location
        if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            // ✅ Arrived at sound, but see no player → Look around
            ChangeState(CikguState.LookingAround);
        }
    }

    void HandleLookingAround()
    {
        // ✅ Look around for [lookAroundTime] seconds
        if (stateTimer >= lookAroundTime)
        {
            // Didn't find anything, start patrolling
            ChangeState(CikguState.Patrolling);
        }
    }

    void HandlePatrolling()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        patrolTimer += Time.deltaTime;

        // Check if need new patrol point
        if (agent.remainingDistance <= agent.stoppingDistance + 0.5f || patrolTimer >= 3f)
        {
            patrolTimer = 0f;
            Vector3 randomPoint = currentSoundTarget + Random.insideUnitSphere * patrolRadius;
            randomPoint.y = transform.position.y;

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(randomPoint, out navHit, patrolRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }

        // Patrol timeout
        if (stateTimer >= patrolTime)
        {
            ChangeState(CikguState.ReturningToSeat);
        }
    }

    void HandleChasing()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (targetPlayer != null)
        {
            agent.SetDestination(targetPlayer.transform.position);

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);

            // ✅ CATCH PLAYER
            if (distToPlayer <= catchDistance)
            {
                CatchPlayer();
            }
        }
        else
        {
            // Lost player, go to last seen position
            if (stateTimer >= 3f)
            {
                ChangeState(CikguState.Patrolling);
            }
        }

        // Check for pending sounds while chasing
        if (hasPendingSound)
        {
            currentSoundTarget = pendingSound;
            hasPendingSound = false;
        }
    }

    void HandleReturning()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // Check if arrived at seat
        if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            transform.position = seatPos;
            transform.rotation = seatRot;

            if (animator != null)
                animator.SetTrigger(ANIM_SIT_DOWN);

            StartCoroutine(SitDownSequence());
        }
    }

    IEnumerator SitDownSequence()
    {
        yield return new WaitForSeconds(0.5f);
        if (PhotonNetwork.IsMasterClient)
            agent.enabled = false;
        ChangeState(CikguState.Sitting);
        HideIndicators();
    }

    // ==================== STATE CHANGES ====================

    void ChangeState(CikguState newState)
    {
        if (currentState == newState) return;

        GameLog.Log($"[CikguNPC] State: {currentState} → {newState}");

        currentState = newState;
        stateTimer = 0f;
        patrolTimer = 0f;

        UpdateAnimator();

        // Enable/disable agent based on state
        if (PhotonNetwork.IsMasterClient && agent != null)
        {
            bool needsAgent = (newState == CikguState.WalkingToSound || 
                              newState == CikguState.Patrolling || 
                              newState == CikguState.Chasing || 
                              newState == CikguState.ReturningToSeat);

            if (needsAgent && !agent.enabled && agent.isOnNavMesh)
            {
                agent.enabled = true;
            }

            // Set speed based on state
            if (newState == CikguState.Chasing)
                agent.speed = chaseSpeed;
            else
                agent.speed = walkSpeed;

            // Set destination for certain states
            if (newState == CikguState.WalkingToSound)
            {
                agent.SetDestination(currentSoundTarget);
                ShowAlert();
            }
            else if (newState == CikguState.ReturningToSeat)
            {
                agent.SetDestination(seatPos);
            }
        }
    }

    // ==================== CATCH PLAYER ====================

    void CatchPlayer()
    {
        if (hasTriggeredGameOver) return;

        hasTriggeredGameOver = true;

        GameLog.Log($"[CikguNPC] CAUGHT PLAYER: {targetPlayer.name}");

        ChangeState(CikguState.Yelling);

        // ✅ Play caught sound ONCE
        if (!hasPlayedCaughtSound)
        {
            PlayCaughtSound();
            hasPlayedCaughtSound = true;
        }

        // Trigger game over via RPC
        photonView.RPC("RPC_TriggerGameOver", RpcTarget.All, targetPlayer.GetComponent<PhotonView>().ViewID);
    }

    [PunRPC]
    void RPC_TriggerGameOver(int caughtPlayerViewID)
    {
        PhotonView caughtView = PhotonView.Find(caughtPlayerViewID);
        if (caughtView != null)
        {
            GameObject caughtPlayer = caughtView.gameObject;
            GameLog.Log($"[RPC_TriggerGameOver] Player caught: {caughtPlayer.name}");

            OnPlayerCaught?.Invoke(caughtPlayer);
        }
    }

    /// <summary>
    /// ✅ RPC called by PlayerVisionDetector when a player is spotted
    /// This allows ALL players (Master and Client) to be detected
    /// </summary>
    [PunRPC]
    void RPC_PlayerSpotted(int playerViewID)
    {
        // Only Master processes spotting
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Ignore if game is over
        if (hasTriggeredGameOver) return;
        if (currentState == CikguState.Yelling) return;
        
        // Find the player by ViewID
        PhotonView playerPV = PhotonView.Find(playerViewID);
        if (playerPV == null)
        {
            Debug.LogWarning($"[CikguNPC] RPC_PlayerSpotted - Player ViewID {playerViewID} not found!");
            return;
        }
        
        GameObject player = playerPV.gameObject;
        
        GameLog.Log($"[CikguNPC] RPC_PlayerSpotted - Chasing {player.name}!");
        
        // Set target
        targetPlayer = player;
        lastSeenPlayerPosition = player.transform.position;
        canSeePlayer = true;
        
        // ✅ Play spotted sound ONCE
        if (!hasPlayedSpottedSound)
        {
            PlaySpottedSound();
            hasPlayedSpottedSound = true;
        }
        
        // Stop any current routine
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        
        // Stand up if sitting
        if (currentState == CikguState.Sitting)
        {
            if (animator != null) animator.SetTrigger(ANIM_STAND_UP);
            currentRoutine = StartCoroutine(StandAndChase());
        }
        else if (currentState != CikguState.Chasing)
        {
            // Already standing, just change to chasing
            ShowExclamation();
            ChangeState(CikguState.Chasing);
        }
    }

    IEnumerator StandAndChase()
    {
        yield return new WaitForSeconds(0.5f);
        if (PhotonNetwork.IsMasterClient) agent.enabled = true;
        ShowExclamation();
        ChangeState(CikguState.Chasing);
    }

    void PlaySpottedSound()
    {
        if (audioSource != null && spottedPlayerClip != null)
        {
            audioSource.PlayOneShot(spottedPlayerClip);
            GameLog.Log("[CikguNPC] Playing spotted sound: Berhenti jangan bergerak!");
        }
    }

    void PlayCaughtSound()
    {
        if (audioSource != null && caughtPlayerClip != null)
        {
            audioSource.PlayOneShot(caughtPlayerClip);
            GameLog.Log("[CikguNPC] Playing caught sound: Apa korang buat ni ha!");
        }
    }

    // ==================== SOUND DETECTION ====================

    void ProcessSound(Vector3 soundPosition, SoundType soundType, float distance, int soundRoom, int soundFloor, GameObject source)
    {
        if (source == gameObject) return;
        if (hasTriggeredGameOver) return;

        // ✅ FILTER 1: Check if allowed sound type
        if (!IsAllowedSound(soundType))
        {
            GameLog.Log($"[CikguNPC] Ignoring sound type: {soundType}");
            return;
        }

        // ✅ FILTER 2: Check if washing machine only mode
        if (washingMachineOnly && !IsWashingMachineSource(source))
        {
            GameLog.Log($"[CikguNPC] Washing machine only mode - ignoring non-washing machine sound");
            return;
        }

        // ✅ Room/floor checks
        if (soundRoom == 0) return;
        if (myFloorLevel != soundFloor)
        {
            GameLog.Log($"[CikguNPC] Different floor - ignoring");
            return;
        }
        if (myRoomID != 0 && myRoomID != soundRoom)
        {
            GameLog.Log($"[CikguNPC] Different room - ignoring");
            return;
        }

        if (currentState == CikguState.Yelling) return;

        GameLog.Log($"[CikguNPC] Reacting to sound: {soundType} from {source?.name} at Room {soundRoom}");

        if (currentState == CikguState.Chasing)
        {
            pendingSound = soundPosition;
            hasPendingSound = true;
            return;
        }

        currentSoundTarget = soundPosition;

        if (currentState == CikguState.Sitting)
        {
            if (animator != null) animator.SetTrigger(ANIM_STAND_UP);
            currentRoutine = StartCoroutine(StandAndGo());
        }
        else
        {
            ChangeState(CikguState.WalkingToSound);
        }
    }

    IEnumerator StandAndGo()
    {
        yield return new WaitForSeconds(0.5f);
        if (PhotonNetwork.IsMasterClient) agent.enabled = true;
        ChangeState(CikguState.WalkingToSound);
    }

    bool IsWashingMachineSource(GameObject source)
    {
        if (source == null) return false;

        if (source.GetComponent<WashingMachine>() != null) return true;
        if (source.GetComponentInParent<WashingMachine>() != null) return true;

        if (!string.IsNullOrEmpty(washingMachineTag) && source.CompareTag(washingMachineTag)) return true;
        if (!string.IsNullOrEmpty(washingMachineTag) && source.transform.root.CompareTag(washingMachineTag)) return true;

        string n = source.name.ToLower();
        string rn = source.transform.root.name.ToLower();
        if (n.Contains("washing") || rn.Contains("washing")) return true;

        return false;
    }

    bool IsAllowedSound(SoundType soundType)
    {
        if (!restrictToAllowedSoundTypes) return true;
        if (allowedSoundTypes == null || allowedSoundTypes.Length == 0) return false;

        for (int i = 0; i < allowedSoundTypes.Length; i++)
            if (allowedSoundTypes[i].Equals(soundType)) return true;

        return false;
    }

    int DetectFloorAtPosition(Vector3 position)
    {
        Collider[] cols = Physics.OverlapSphere(position, 0.5f);
        foreach (var c in cols)
        {
            RoomTrigger room = c.GetComponent<RoomTrigger>();
            if (room != null) return room.floorLevel;
        }

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null) return room.floorLevel;
        }

        return myFloorLevel;
    }

    // ==================== ISoundListener INTERFACE ====================

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void OnSoundHeard(Vector3 soundPosition, SoundType soundType, float distance, GameObject source)
    {
        // ✅ This is called by player footsteps - we IGNORE these
        GameLog.Log($"[CikguNPC] OnSoundHeard (footsteps) - IGNORED");
        return;
    }

    public void OnSoundHeardWithRoom(Vector3 soundPosition, SoundType soundType, float distance, int roomID, GameObject source)
    {
        // ✅ This is called by washing machine and thrown items - we PROCESS these
        if (!PhotonNetwork.IsMasterClient) return; // Only master processes sounds

        int floorLevel = DetectFloorAtPosition(soundPosition);
        ProcessSound(soundPosition, soundType, distance, roomID, floorLevel, source);
    }

    // ==================== PHOTON SYNC ====================

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ✅ MASTER: Send state to clients
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext((int)currentState);
        }
        else
        {
            // ✅ CLIENT: Receive state from master
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkState = (CikguState)stream.ReceiveNext();
        }
    }

    // ==================== ANIMATION ====================

    void UpdateAnimator()
    {
        if (animator == null) return;

        // ✅ Reset ALL animation parameters
        animator.SetBool(ANIM_SITTING, false);
        animator.SetBool(ANIM_WALKING, false);
        animator.SetBool(ANIM_RUNNING, false);
        animator.SetBool(ANIM_LOOKING, false);
        animator.SetBool(ANIM_YELLING, false);

        // ✅ Set ONE parameter based on current state
        switch (currentState)
        {
            case CikguState.Sitting:
                animator.SetBool(ANIM_SITTING, true);
                GameLog.Log("[CikguNPC] Animation: Sitting");
                break;

            case CikguState.WalkingToSound:
            case CikguState.ReturningToSeat:
            case CikguState.Patrolling:
                animator.SetBool(ANIM_WALKING, true);
                GameLog.Log("[CikguNPC] Animation: Walking");
                break;

            case CikguState.LookingAround:
                animator.SetBool(ANIM_LOOKING, true);
                GameLog.Log("[CikguNPC] Animation: Looking Around");
                break;

            case CikguState.Chasing:
                animator.SetBool(ANIM_RUNNING, true);
                GameLog.Log("[CikguNPC] Animation: Running");
                break;

            case CikguState.Yelling:
                animator.SetBool(ANIM_YELLING, true);
                GameLog.Log("[CikguNPC] Animation: Yelling");
                break;
        }
    }

    // ==================== INDICATORS ====================

    void UpdateIndicators()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (alertIndicator != null && alertIndicator.activeSelf)
        {
            alertIndicator.transform.LookAt(cam.transform);
            alertIndicator.transform.Rotate(0, 180, 0);
        }
        if (exclamationIndicator != null && exclamationIndicator.activeSelf)
        {
            exclamationIndicator.transform.LookAt(cam.transform);
            exclamationIndicator.transform.Rotate(0, 180, 0);
        }
    }

    void ShowAlert()
    {
        if (alertIndicator != null) alertIndicator.SetActive(true);
        if (exclamationIndicator != null) exclamationIndicator.SetActive(false);
    }

    void ShowExclamation()
    {
        if (alertIndicator != null) alertIndicator.SetActive(false);
        if (exclamationIndicator != null) exclamationIndicator.SetActive(true);
    }

    void HideIndicators()
    {
        if (alertIndicator != null) alertIndicator.SetActive(false);
        if (exclamationIndicator != null) exclamationIndicator.SetActive(false);
    }

    // ==================== DEBUG ====================

    void OnDrawGizmosSelected()
    {
        // Draw vision cone
        if (hasVision && currentState != CikguState.Yelling)
        {
            Gizmos.color = Color.yellow;
            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 forward = transform.forward * visionRange;
            
            Gizmos.DrawRay(eyePos, forward);
            
            Vector3 leftDir = Quaternion.Euler(0, -visionAngle / 2f, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, visionAngle / 2f, 0) * forward;
            
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawRay(eyePos, leftDir);
            Gizmos.DrawRay(eyePos, rightDir);
        }

        // Draw catch distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        // Draw sound target when investigating
        if (currentState == CikguState.WalkingToSound || currentState == CikguState.LookingAround)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentSoundTarget, 0.5f);
            Gizmos.DrawLine(transform.position, currentSoundTarget);
        }
    }
}