using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Photon.Pun;
using TMPro;

public enum CikguState
{
    Sitting,
    WalkingToSound,
    LookingAround,      // Looking around at sound / last-known-player location
    Patrolling,
    Chasing,
    Yelling,            // Caught a player (game over)
    ReturningToSeat
}

/// <summary>
/// CikguNPC - the Level 2 warden ("teacher").
///
/// AI runs ONLY on the Master Client; clients receive position/rotation/state
/// via OnPhotonSerializeView and lerp.
///
/// Behaviour:
/// - Sits at her seat watching the room with a vision cone.
/// - Seeing a player builds SUSPICION (faster the closer they are).
///   * Above investigateThreshold she stands up and checks the spot.
///   * At 100 (or inside instantSpotRange) she chases.
/// - Loud/medium sounds in her room (washing machine, thrown items, running
///   footsteps) make her investigate the sound location.
/// - While chasing, if she loses line of sight for loseSightTime seconds she
///   walks to the player's last known position, looks around, patrols the
///   area, then gives up and returns to her seat. Escape is possible.
/// - Catching a player (within catchDistance) triggers team game over.
///
/// Sounds can be reported from ANY client via OnSoundHeardWithRoom /
/// ReportSound - they are forwarded to the Master Client with an RPC.
/// </summary>
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
    [Tooltip("Hard cap applied on top of visionRange (keeps old scenes with visionRange=100 playable)")]
    public float sightRangeCap = 18f;
    [Tooltip("How often to check for players (seconds)")]
    public float visionCheckInterval = 0.2f;
    public LayerMask visionBlockingLayers;
    public LayerMask playerLayer;

    [Header("Suspicion")]
    [Tooltip("Seeing a player inside this range = instantly spotted")]
    public float instantSpotRange = 4.5f;
    [Tooltip("Suspicion gained per second while a player is visible at point-blank range (scales down with distance)")]
    public float suspicionRiseRate = 55f;
    [Tooltip("Suspicion lost per second while no player is visible")]
    public float suspicionDecayRate = 12f;
    [Tooltip("At this suspicion (0-100) she stands up and investigates the glimpse")]
    public float investigateThreshold = 45f;

    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float chaseSpeed = 6f;

    [Header("Chase / Search")]
    [Tooltip("Seconds without line of sight before she breaks off a chase and searches")]
    public float loseSightTime = 3.5f;

    [Header("Timing Settings")]
    public float lookAroundTime = 3f;      // Time spent looking around at sound location
    public float patrolTime = 10f;
    public float patrolRadius = 8f;

    [Header("Catch Settings")]
    [Tooltip("Distance to catch player")]
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

    [Header("Footsteps")]
    [Tooltip("React to player footsteps (walking/running) near her")]
    public bool hearFootsteps = true;
    [Tooltip("Max distance at which walking/running footsteps alert her")]
    public float footstepHearingRange = 8f;
    [Tooltip("Distance where normal walking footsteps can alert Cikgu")]
    public float mediumFootstepHearingRange = 7f;
    [Tooltip("Distance where running, jumping, and landing can alert Cikgu")]
    public float loudFootstepHearingRange = 13f;

    [Header("Indicators")]
    [Tooltip("Create simple world-space ? and ! indicators if none are assigned.")]
    public bool autoCreateIndicators = true;
    public GameObject alertIndicator;
    public GameObject exclamationIndicator;
    public float indicatorHeight = 2.2f;

    [Header("References")]
    public Animator animator;
    public AudioSource audioSource;

    [Header("Audio")]
    [Tooltip("Played when spotting player: 'Berhenti jangan bergerak!'")]
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
    private float footstepReportCooldown = 0f;

    // Player tracking
    private GameObject targetPlayer;
    private Vector3 lastSeenPlayerPosition;
    private bool canSeeTarget = false;
    private float visionCheckTimer;
    private float loseSightTimer = 0f;

    // Suspicion (0..100, master only; synced for UI/debug)
    private float suspicion = 0f;

    // Timers
    private float stateTimer = 0f;
    private float patrolTimer = 0f;

    // Animation parameters (must match the Animator Controller)
    private const string ANIM_SITTING = "IsSitting";
    private const string ANIM_WALKING = "IsWalking";
    private const string ANIM_RUNNING = "IsRunning";
    private const string ANIM_LOOKING = "IsLooking";
    private const string ANIM_YELLING = "IsYelling";
    private const string ANIM_STAND_UP = "StandUp";
    private const string ANIM_SIT_DOWN = "SitDown";

    private Coroutine currentRoutine;
    private bool standTransitionPending = false;
    private bool sitTransitionPending = false;

    // Network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private CikguState networkState;
    private float networkSuspicion;
    private float lerpSpeed = 10f;

    // Game over tracking
    private bool hasTriggeredGameOver = false;
    private bool hasPlayedSpottedSound = false;
    private bool hasPlayedCaughtSound = false;

    public static System.Action<GameObject> OnPlayerCaught;

    /// <summary>Current suspicion 0-100 (synced to clients) - usable for UI.</summary>
    public float Suspicion => PhotonNetwork.IsMasterClient ? suspicion : networkSuspicion;
    public CikguState CurrentState => currentState;

    void Awake()
    {
        hasTriggeredGameOver = false;
        currentState = CikguState.Sitting;
        targetPlayer = null;
        canSeeTarget = false;
        suspicion = 0f;
        standTransitionPending = false;
        sitTransitionPending = false;
        hasPlayedSpottedSound = false;
        hasPlayedCaughtSound = false;

        GameLog.Log("[CikguNPC] Awake - State reset");
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

        if (PhotonNetwork.IsMasterClient)
        {
            agent.speed = walkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = true;
            agent.enabled = false; // Start sitting
        }
        else
        {
            agent.enabled = false; // Clients lerp to network position
        }

        SetupIndicators();

        if (myRoomID == 0) DetectMyRoom();

        // Register for local sound events (footsteps etc.) on EVERY client -
        // reports are forwarded to the Master via RPC.
        if (SoundDetectionSystem.Instance != null)
            SoundDetectionSystem.Instance.RegisterListener(this);

        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkState = CikguState.Sitting;

        currentState = CikguState.Sitting;
        UpdateAnimator();

        if (playerLayer.value == 0)
            playerLayer = LayerMask.GetMask("Player");

        GameLog.Log($"[CikguNPC] Initialized - Vision: {hasVision}, Room: {myRoomID}, Floor: {myFloorLevel}, IsMaster: {PhotonNetwork.IsMasterClient}");
    }

    void OnDestroy()
    {
        if (SoundDetectionSystem.Instance != null)
            SoundDetectionSystem.Instance.UnregisterListener(this);
    }

    void SetupIndicators()
    {
        if (autoCreateIndicators)
        {
            if (alertIndicator == null)
                alertIndicator = CreateIndicator("CikguAlertIndicator", "?", new Color(1f, 0.85f, 0.2f, 1f));

            if (exclamationIndicator == null)
                exclamationIndicator = CreateIndicator("CikguExclamationIndicator", "!", new Color(1f, 0.2f, 0.15f, 1f));
        }

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

    GameObject CreateIndicator(string objectName, string text, Color color)
    {
        GameObject indicator = new GameObject(objectName);
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = Vector3.up * indicatorHeight;
        indicator.transform.localScale = Vector3.one * 0.35f;

        TextMeshPro label = indicator.AddComponent<TextMeshPro>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 8f;
        label.color = color;
        label.enableWordWrapping = false;

        return indicator;
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
        if (footstepReportCooldown > 0f)
            footstepReportCooldown -= Time.deltaTime;

        if (PhotonNetwork.IsMasterClient)
            MasterUpdate();
        else
            ClientUpdate();

        MaintainAnimatorState();
        UpdateIndicators();
    }

    void MasterUpdate()
    {
        stateTimer += Time.deltaTime;

        // Vision runs in every state except Yelling / after game over
        if (hasVision && currentState != CikguState.Yelling && !hasTriggeredGameOver)
        {
            visionCheckTimer -= Time.deltaTime;
            if (visionCheckTimer <= 0f)
            {
                visionCheckTimer = visionCheckInterval;
                VisionTick(visionCheckInterval);
            }
        }

        switch (currentState)
        {
            case CikguState.Sitting:
                break; // Just sit and watch

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
                break; // Animation playing

            case CikguState.ReturningToSeat:
                HandleReturning();
                break;
        }
    }

    void ClientUpdate()
    {
        if (currentState != networkState)
        {
            currentState = networkState;
            UpdateAnimator();
        }

        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
    }

    // ==================== VISION & SUSPICION ====================

    float EffectiveSightRange => Mathf.Min(visionRange, sightRangeCap);

    void VisionTick(float dt)
    {
        GameObject visiblePlayer = FindClosestVisiblePlayer(out float visibleDistance);

        if (currentState == CikguState.Chasing)
        {
            // While chasing, "visible" is checked against a wider leash so she
            // doesn't instantly forget a player right in front of her.
            if (targetPlayer != null && CanSeePosition(targetPlayer.transform.position, EffectiveSightRange * 1.25f, 360f))
            {
                canSeeTarget = true;
                loseSightTimer = 0f;
                lastSeenPlayerPosition = targetPlayer.transform.position;
            }
            else
            {
                canSeeTarget = false;
            }
            return;
        }

        if (visiblePlayer != null)
        {
            lastSeenPlayerPosition = visiblePlayer.transform.position;

            if (visibleDistance <= instantSpotRange)
            {
                suspicion = 100f;
            }
            else
            {
                float proximity = 1f - Mathf.Clamp01(visibleDistance / Mathf.Max(EffectiveSightRange, 0.01f));
                suspicion += suspicionRiseRate * (0.35f + 0.65f * proximity) * dt;
            }

            if (suspicion >= 100f)
            {
                suspicion = 100f;
                bool caughtImmediately = visibleDistance <= catchDistance + 0.35f;
                StartChase(visiblePlayer, !caughtImmediately);

                if (caughtImmediately)
                    CatchPlayer();
            }
            else if (suspicion >= investigateThreshold && CanStartInvestigating())
            {
                GameLog.Log($"<color=yellow>[CikguNPC] Suspicious ({suspicion:F0}) - checking glimpse location</color>");
                currentSoundTarget = lastSeenPlayerPosition;
                BeginInvestigate();
            }
        }
        else
        {
            suspicion = Mathf.Max(0f, suspicion - suspicionDecayRate * dt);
        }
    }

    bool CanStartInvestigating()
    {
        return currentState == CikguState.Sitting ||
               currentState == CikguState.LookingAround ||
               currentState == CikguState.Patrolling ||
               currentState == CikguState.ReturningToSeat;
    }

    GameObject FindClosestVisiblePlayer(out float closestDistance)
    {
        closestDistance = float.MaxValue;
        GameObject closest = null;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player == null) continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist >= closestDistance) continue;

            if (CanSeePosition(player.transform.position, EffectiveSightRange, visionAngle))
            {
                closest = player;
                closestDistance = dist;
            }
        }

        return closest;
    }

    bool CanSeePosition(Vector3 worldPos, float range, float angleLimit)
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Vector3 targetPos = worldPos + Vector3.up * 1f;

        float dist = Vector3.Distance(eyePos, targetPos);
        if (dist > range) return false;

        Vector3 dir = (targetPos - eyePos).normalized;
        if (angleLimit < 360f)
        {
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > angleLimit / 2f) return false;
        }

        // Blocked if any wall/obstacle sits between her eyes and the target.
        // Players are NOT on the blocking layers, so a clear ray = visible.
        if (Physics.Raycast(eyePos, dir, dist - 0.3f, visionBlockingLayers, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    // ==================== STATE HANDLERS ====================

    void HandleWalkingToSound()
    {
        if (!AgentReady()) return;
        if (agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            // Arrived at the spot but see no player - look around
            ChangeState(CikguState.LookingAround);
        }
    }

    void HandleLookingAround()
    {
        if (stateTimer >= lookAroundTime)
        {
            // Didn't find anything, sweep the area
            ChangeState(CikguState.Patrolling);
        }
    }

    void HandlePatrolling()
    {
        if (!AgentReady()) return;

        patrolTimer += Time.deltaTime;

        if (!agent.pathPending &&
            (agent.remainingDistance <= agent.stoppingDistance + 0.5f || patrolTimer >= 3f))
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

        if (stateTimer >= patrolTime)
        {
            ChangeState(CikguState.ReturningToSeat);
        }
    }

    void HandleChasing()
    {
        if (!AgentReady()) return;

        if (targetPlayer == null)
        {
            GiveUpChase();
            return;
        }

        if (canSeeTarget)
        {
            agent.SetDestination(targetPlayer.transform.position);
        }
        else
        {
            // Chase the last known position
            agent.SetDestination(lastSeenPlayerPosition);
            loseSightTimer += Time.deltaTime;

            if (loseSightTimer >= loseSightTime)
            {
                GiveUpChase();
                return;
            }
        }

        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        if (distToPlayer <= catchDistance)
        {
            CatchPlayer();
        }

        // Remember pending sounds heard during the chase
        if (hasPendingSound)
        {
            currentSoundTarget = pendingSound;
            hasPendingSound = false;
        }
    }

    void GiveUpChase()
    {
        GameLog.Log("<color=orange>[CikguNPC] Lost the player - searching last known position</color>");

        targetPlayer = null;
        canSeeTarget = false;
        suspicion = Mathf.Min(suspicion, investigateThreshold + 10f); // Stays wary
        hasPlayedSpottedSound = false;

        // Search where she last saw them: walk there, look around, patrol, give up
        currentSoundTarget = lastSeenPlayerPosition;
        ShowAlert();
        ChangeState(CikguState.WalkingToSound);
    }

    void HandleReturning()
    {
        if (!AgentReady()) return;
        if (agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            if (sitTransitionPending) return;

            transform.position = seatPos;
            transform.rotation = seatRot;

            if (animator != null)
            {
                ClearLocomotionBools();
                animator.ResetTrigger(ANIM_STAND_UP);
                animator.SetTrigger(ANIM_SIT_DOWN);
            }

            sitTransitionPending = true;
            StartCoroutine(SitDownSequence());
        }
    }

    IEnumerator SitDownSequence()
    {
        yield return new WaitForSeconds(0.5f);
        if (PhotonNetwork.IsMasterClient && agent != null)
            agent.enabled = false;
        sitTransitionPending = false;
        ChangeState(CikguState.Sitting);
        HideIndicators();
    }

    // ==================== NAVMESH AGENT ====================

    /// <summary>
    /// Makes sure the agent is enabled AND actually on the NavMesh.
    /// Her seat can be slightly off the mesh (sitting on a chair), which used
    /// to silently break all movement - now we warp to the nearest mesh point.
    /// </summary>
    bool AgentReady()
    {
        if (agent == null) return false;

        if (!agent.enabled)
            agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError("[CikguNPC] No NavMesh within 3m of Cikgu - she cannot move! Re-bake the NavMesh or move her seat.");
                return false;
            }
        }

        return true;
    }

    // ==================== STATE CHANGES ====================

    void ChangeState(CikguState newState)
    {
        if (currentState == newState)
        {
            if (!standTransitionPending && !sitTransitionPending)
                UpdateAnimator();
            return;
        }

        GameLog.Log($"[CikguNPC] State: {currentState} → {newState}");

        currentState = newState;
        stateTimer = 0f;
        patrolTimer = 0f;
        standTransitionPending = false;
        sitTransitionPending = false;

        UpdateAnimator();

        if (!PhotonNetwork.IsMasterClient || agent == null) return;

        bool needsAgent = (newState == CikguState.WalkingToSound ||
                          newState == CikguState.Patrolling ||
                          newState == CikguState.Chasing ||
                          newState == CikguState.ReturningToSeat);

        if (needsAgent && !AgentReady()) return;

        if (needsAgent)
            agent.isStopped = false;

        if (newState == CikguState.Chasing)
        {
            agent.speed = chaseSpeed;
            loseSightTimer = 0f;
        }
        else
        {
            agent.speed = walkSpeed;
        }

        if (newState == CikguState.WalkingToSound)
        {
            agent.SetDestination(currentSoundTarget);
            ShowAlert();
        }
        else if (newState == CikguState.ReturningToSeat)
        {
            agent.SetDestination(seatPos);
            HideIndicators();
        }
    }

    void StartChase(GameObject player, bool playSpottedSound = true)
    {
        if (player == null || hasTriggeredGameOver) return;
        if (currentState == CikguState.Chasing || currentState == CikguState.Yelling) return;

        GameLog.Log($"<color=red>[CikguNPC] SPOTTED {player.name} - CHASING!</color>");

        targetPlayer = player;
        lastSeenPlayerPosition = player.transform.position;
        canSeeTarget = true;
        suspicion = 100f;

        if (playSpottedSound)
            PlaySpottedSound();

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (currentState == CikguState.Sitting)
        {
            if (animator != null)
            {
                ClearLocomotionBools();
                animator.ResetTrigger(ANIM_SIT_DOWN);
                animator.SetTrigger(ANIM_STAND_UP);
            }
            standTransitionPending = true;
            currentRoutine = StartCoroutine(StandThen(CikguState.Chasing));
        }
        else
        {
            ShowExclamation();
            ChangeState(CikguState.Chasing);
        }
    }

    void BeginInvestigate()
    {
        if (currentState == CikguState.Sitting)
        {
            if (animator != null)
            {
                ClearLocomotionBools();
                animator.ResetTrigger(ANIM_SIT_DOWN);
                animator.SetTrigger(ANIM_STAND_UP);
            }
            standTransitionPending = true;
            currentRoutine = StartCoroutine(StandThen(CikguState.WalkingToSound));
        }
        else
        {
            ChangeState(CikguState.WalkingToSound);
        }
    }

    IEnumerator StandThen(CikguState nextState)
    {
        yield return new WaitForSeconds(0.5f);
        if (hasTriggeredGameOver || currentState == CikguState.Yelling)
        {
            standTransitionPending = false;
            yield break;
        }

        if (PhotonNetwork.IsMasterClient && agent != null) agent.enabled = true;
        if (nextState == CikguState.Chasing) ShowExclamation();
        standTransitionPending = false;
        ChangeState(nextState);
    }

    // ==================== CATCH PLAYER ====================

    void CatchPlayer()
    {
        if (hasTriggeredGameOver) return;
        if (targetPlayer == null) return;

        PhotonView targetView = targetPlayer.GetComponent<PhotonView>();
        if (targetView == null)
        {
            Debug.LogError($"[CikguNPC] Caught player {targetPlayer.name} has no PhotonView!");
            return;
        }

        hasTriggeredGameOver = true;
        standTransitionPending = false;
        sitTransitionPending = false;

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        GameLog.Log($"[CikguNPC] CAUGHT PLAYER: {targetPlayer.name}");

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

        ChangeState(CikguState.Yelling);

        photonView.RPC("RPC_TriggerGameOver", RpcTarget.All, targetView.ViewID);
    }

    [PunRPC]
    void RPC_TriggerGameOver(int caughtPlayerViewID)
    {
        // Caught sound plays on every client
        if (!hasPlayedCaughtSound)
        {
            PlayCaughtSound();
            hasPlayedCaughtSound = true;
        }

        PhotonView caughtView = PhotonView.Find(caughtPlayerViewID);
        if (caughtView != null)
        {
            GameObject caughtPlayer = caughtView.gameObject;
            GameLog.Log($"[RPC_TriggerGameOver] Player caught: {caughtPlayer.name}");

            OnPlayerCaught?.Invoke(caughtPlayer);
        }
    }

    /// <summary>
    /// Legacy hook (was sent by PlayerVisionDetector). Still supported:
    /// treated as a confirmed sighting.
    /// </summary>
    [PunRPC]
    void RPC_PlayerSpotted(int playerViewID)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (hasTriggeredGameOver) return;
        if (currentState == CikguState.Yelling) return;

        PhotonView playerPV = PhotonView.Find(playerViewID);
        if (playerPV == null) return;

        StartChase(playerPV.gameObject);
    }

    void PlaySpottedSound()
    {
        if (photonView != null && PhotonNetwork.InRoom)
            photonView.RPC("RPC_PlaySpottedSound", RpcTarget.All);
        else
            RPC_PlaySpottedSound();
    }

    [PunRPC]
    void RPC_PlaySpottedSound()
    {
        if (hasTriggeredGameOver || hasPlayedCaughtSound || hasPlayedSpottedSound) return;

        if (audioSource != null && spottedPlayerClip != null && !audioSource.isPlaying)
        {
            audioSource.PlayOneShot(spottedPlayerClip);
            hasPlayedSpottedSound = true;
        }
    }

    void PlayCaughtSound()
    {
        if (audioSource != null && caughtPlayerClip != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(caughtPlayerClip);
        }
    }

    // ==================== SOUND DETECTION ====================

    /// <summary>
    /// Report a sound to this Cikgu from ANY client. Forwarded to the Master
    /// Client, where the AI decides whether to react.
    /// roomID &lt;= 0 means "detect the room from the position" (master-side).
    /// </summary>
    public void ReportSound(Vector3 soundPosition, SoundType soundType, int roomID, bool isWashingMachine)
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_ReportSound", RpcTarget.MasterClient,
                soundPosition, (int)soundType, roomID, isWashingMachine);
        }
        else
        {
            RPC_ReportSound(soundPosition, (int)soundType, roomID, isWashingMachine);
        }
    }

    [PunRPC]
    void RPC_ReportSound(Vector3 soundPosition, int soundTypeInt, int roomID, bool isWashingMachine)
    {
        if (!PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom) return;

        if (roomID <= 0)
            roomID = DetectRoomAtPosition(soundPosition);

        int floorLevel = DetectFloorAtPosition(soundPosition);
        float distance = Vector3.Distance(transform.position, soundPosition);

        ProcessSound(soundPosition, (SoundType)soundTypeInt, distance, roomID, floorLevel, isWashingMachine);
    }

    void ProcessSound(Vector3 soundPosition, SoundType soundType, float distance, int soundRoom, int soundFloor, bool isWashingMachine)
    {
        if (hasTriggeredGameOver) return;
        if (currentState == CikguState.Yelling) return;

        if (!IsAllowedSound(soundType))
            return;

        if (washingMachineOnly && !isWashingMachine)
            return;

        // Room/floor gating: she only cares about her own room
        if (soundRoom == 0) return;
        if (myFloorLevel != soundFloor) return;
        if (myRoomID != 0 && myRoomID != soundRoom) return;

        GameLog.Log($"<color=yellow>[CikguNPC] Heard {soundType} in Room {soundRoom} ({distance:F1}m) - investigating</color>");

        if (currentState == CikguState.Chasing)
        {
            pendingSound = soundPosition;
            hasPendingSound = true;
            return;
        }

        // Sounds also make her more wary (but can never trigger a chase alone)
        if (suspicion < 60f)
            suspicion = Mathf.Min(60f, suspicion + 25f);

        currentSoundTarget = soundPosition;

        if (currentState == CikguState.Sitting)
        {
            if (animator != null)
            {
                ClearLocomotionBools();
                animator.ResetTrigger(ANIM_SIT_DOWN);
                animator.SetTrigger(ANIM_STAND_UP);
            }
            standTransitionPending = true;
            if (currentRoutine != null) StopCoroutine(currentRoutine);
            currentRoutine = StartCoroutine(StandThen(CikguState.WalkingToSound));
        }
        else if (currentState == CikguState.WalkingToSound)
        {
            // Newer sound wins - retarget
            if (AgentReady()) agent.SetDestination(currentSoundTarget);
            stateTimer = 0f;
        }
        else
        {
            ChangeState(CikguState.WalkingToSound);
        }
    }

    bool IsAllowedSound(SoundType soundType)
    {
        if (!restrictToAllowedSoundTypes) return true;
        if (allowedSoundTypes == null || allowedSoundTypes.Length == 0) return false;

        for (int i = 0; i < allowedSoundTypes.Length; i++)
            if (allowedSoundTypes[i].Equals(soundType)) return true;

        return false;
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

    int DetectRoomAtPosition(Vector3 position)
    {
        Collider[] cols = Physics.OverlapSphere(position, 0.5f);
        foreach (var c in cols)
        {
            RoomTrigger room = c.GetComponent<RoomTrigger>();
            if (room != null) return room.roomID;
        }

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null) return room.roomID;
        }

        return 0;
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

    /// <summary>
    /// Local sounds (player footsteps, jumps, dropped items) from
    /// SoundDetectionSystem. Fires on the client that emitted the sound -
    /// forwarded to the Master via ReportSound.
    /// </summary>
    public void OnSoundHeard(Vector3 soundPosition, SoundType soundType, float distance, GameObject source)
    {
        if (!hearFootsteps) return;
        if (source == gameObject) return;
        if (soundType == SoundType.Silent || soundType == SoundType.VeryQuiet || soundType == SoundType.Quiet) return;

        float hearingRange = GetFootstepHearingRange(soundType);
        if (distance > hearingRange) return;

        // Washing machine reports itself with room info - don't double-report
        if (IsWashingMachineSource(source)) return;

        // Rate-limit RPC spam from rapid footsteps
        if (footstepReportCooldown > 0f) return;
        footstepReportCooldown = 0.5f;

        ReportSound(soundPosition, soundType, -1, false);
    }

    float GetFootstepHearingRange(SoundType soundType)
    {
        if (soundType == SoundType.Loud)
            return Mathf.Max(loudFootstepHearingRange, footstepHearingRange);

        if (soundType == SoundType.Medium)
            return mediumFootstepHearingRange;

        return 0f;
    }

    /// <summary>
    /// Sounds with explicit room info (washing machine, thrown items).
    /// Safe to call from any client.
    /// </summary>
    public void OnSoundHeardWithRoom(Vector3 soundPosition, SoundType soundType, float distance, int roomID, GameObject source)
    {
        ReportSound(soundPosition, soundType, roomID, IsWashingMachineSource(source));
    }

    // ==================== PHOTON SYNC ====================

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext((int)currentState);
            stream.SendNext(suspicion);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkState = (CikguState)stream.ReceiveNext();
            networkSuspicion = (float)stream.ReceiveNext();
        }
    }

    // ==================== ANIMATION ====================

    void MaintainAnimatorState()
    {
        if (animator == null) return;
        if (standTransitionPending || sitTransitionPending) return;

        if (!AnimatorMatchesState())
            UpdateAnimator();
    }

    bool AnimatorMatchesState()
    {
        bool isSitting = animator.GetBool(ANIM_SITTING);
        bool isWalking = animator.GetBool(ANIM_WALKING);
        bool isRunning = animator.GetBool(ANIM_RUNNING);
        bool isLooking = animator.GetBool(ANIM_LOOKING);
        bool isYelling = animator.GetBool(ANIM_YELLING);

        switch (currentState)
        {
            case CikguState.Sitting:
                return isSitting && !isWalking && !isRunning && !isLooking && !isYelling;

            case CikguState.WalkingToSound:
            case CikguState.ReturningToSeat:
            case CikguState.Patrolling:
                return !isSitting && isWalking && !isRunning && !isLooking && !isYelling;

            case CikguState.LookingAround:
                return !isSitting && !isWalking && !isRunning && isLooking && !isYelling;

            case CikguState.Chasing:
                return !isSitting && !isWalking && isRunning && !isLooking && !isYelling;

            case CikguState.Yelling:
                return !isSitting && !isWalking && !isRunning && !isLooking && isYelling;
        }

        return true;
    }

    /// <summary>
    /// Clears every locomotion/pose bool. Called right before firing the
    /// StandUp / SitDown triggers so the AnyState bool-transitions don't
    /// immediately yank her back out of the stand/sit bridge state.
    /// </summary>
    void ClearLocomotionBools()
    {
        if (animator == null) return;
        animator.SetBool(ANIM_SITTING, false);
        animator.SetBool(ANIM_WALKING, false);
        animator.SetBool(ANIM_RUNNING, false);
        animator.SetBool(ANIM_LOOKING, false);
        animator.SetBool(ANIM_YELLING, false);
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool(ANIM_SITTING, false);
        animator.SetBool(ANIM_WALKING, false);
        animator.SetBool(ANIM_RUNNING, false);
        animator.SetBool(ANIM_LOOKING, false);
        animator.SetBool(ANIM_YELLING, false);

        switch (currentState)
        {
            case CikguState.Sitting:
                animator.SetBool(ANIM_SITTING, true);
                break;

            case CikguState.WalkingToSound:
            case CikguState.ReturningToSeat:
            case CikguState.Patrolling:
                animator.SetBool(ANIM_WALKING, true);
                break;

            case CikguState.LookingAround:
                animator.SetBool(ANIM_LOOKING, true);
                break;

            case CikguState.Chasing:
                animator.SetBool(ANIM_RUNNING, true);
                break;

            case CikguState.Yelling:
                animator.SetBool(ANIM_YELLING, true);
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
        if (hasVision && currentState != CikguState.Yelling)
        {
            float range = Mathf.Min(visionRange, sightRangeCap);
            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 forward = transform.forward * range;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(eyePos, forward);

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawRay(eyePos, Quaternion.Euler(0, -visionAngle / 2f, 0) * forward);
            Gizmos.DrawRay(eyePos, Quaternion.Euler(0, visionAngle / 2f, 0) * forward);

            // Instant-spot bubble
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, instantSpotRange);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        // Footstep hearing radius
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, footstepHearingRange);

        if (currentState == CikguState.WalkingToSound || currentState == CikguState.LookingAround || currentState == CikguState.Patrolling)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentSoundTarget, 0.5f);
            Gizmos.DrawLine(transform.position, currentSoundTarget);
        }
    }
}
