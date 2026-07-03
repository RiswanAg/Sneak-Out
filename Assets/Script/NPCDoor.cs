using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Door yang boleh dibuka oleh NPC dan Player
/// Automatically handles NavMesh when door opens/closes
/// </summary>
public class NPCDoor : MonoBehaviour
{
    [Header("Door Settings")]
    public Transform doorPivot;           // The door object to rotate
    public float openAngle = 90f;         // How far door opens
    public float openSpeed = 3f;          // Animation speed
    
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 2f;
    
    [Header("NavMesh")]
    [Tooltip("NavMeshObstacle on the door - will be disabled when open")]
    public NavMeshObstacle navObstacle;
    
    [Header("Collider")]
    [Tooltip("Door collider to disable when open (so player can pass through)")]
    public Collider doorCollider;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    
    // State
    private bool isOpen = false;
    private bool playerNear = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    
    // For NPC access
    public bool IsOpen => isOpen;
    
    void Start()
    {
        if (doorPivot == null)
            doorPivot = transform;
        
        closedRotation = doorPivot.localRotation;
        openRotation = Quaternion.Euler(doorPivot.localEulerAngles + new Vector3(0, openAngle, 0));
        
        // Setup NavMeshObstacle
        if (navObstacle == null)
            navObstacle = GetComponent<NavMeshObstacle>();
        
        // Auto-find door collider on pivot if not assigned
        if (doorCollider == null && doorPivot != null)
        {
            doorCollider = doorPivot.GetComponent<Collider>();
            // Skip if it's a trigger (we don't want to disable triggers)
            if (doorCollider != null && doorCollider.isTrigger)
                doorCollider = null;
        }
        
        // Make sure obstacle is enabled when door is closed
        if (navObstacle != null)
            navObstacle.carving = !isOpen;
        
        // Make sure collider matches door state
        if (doorCollider != null)
            doorCollider.enabled = !isOpen;
    }
    
    void Update()
    {
        // Player interaction
        if (playerNear && Input.GetKeyDown(interactKey))
        {
            ToggleDoor();
        }
        
        // Animate door
        AnimateDoor();
    }
    
    void AnimateDoor()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        doorPivot.localRotation = Quaternion.Slerp(doorPivot.localRotation, targetRotation, Time.deltaTime * openSpeed);
    }
    
    /// <summary>
    /// Toggle door open/close
    /// </summary>
    public void ToggleDoor()
    {
        isOpen = !isOpen;
        UpdateNavMesh();
        PlaySound();
        
        Debug.Log($"Door {gameObject.name}: {(isOpen ? "OPENED" : "CLOSED")}");
    }
    
    /// <summary>
    /// Open the door (for NPC use)
    /// </summary>
    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;
            UpdateNavMesh();
            PlaySound();
            Debug.Log($"Door {gameObject.name}: OPENED by NPC");
        }
    }
    
    /// <summary>
    /// Close the door
    /// </summary>
    public void CloseDoor()
    {
        if (isOpen)
        {
            isOpen = false;
            UpdateNavMesh();
            PlaySound();
            Debug.Log($"Door {gameObject.name}: CLOSED");
        }
    }
    
    void UpdateNavMesh()
    {
        // Disable NavMesh obstacle when open
        if (navObstacle != null)
        {
            navObstacle.carving = !isOpen;
        }
        
        // Disable door collider when open so player can walk through
        if (doorCollider != null)
        {
            doorCollider.enabled = !isOpen;
        }
    }
    
    void PlaySound()
    {
        if (audioSource != null)
        {
            AudioClip clip = isOpen ? openSound : closeSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
    
    // Player detection
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = true;
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = false;
    }
    
    // Visualize in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}