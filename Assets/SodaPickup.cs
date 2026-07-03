using UnityEngine;
using Photon.Pun;

/// <summary>
/// Soda Pickup Item - Multiplayer Synced
/// When picked up: Stamina becomes unlimited (no drain) for 10 seconds
/// Disappears for ALL players when one player picks it up
/// </summary>
public class SodaPickup : MonoBehaviourPunCallbacks
{
    [Header("Boost Settings")]
    [Tooltip("Duration of unlimited stamina in seconds")]
    public float boostDuration = 10f;
    
    [Header("Visual Feedback")]
    [Tooltip("Particle effect when picked up")]
    public GameObject pickupEffect;
    
    [Tooltip("Floating animation speed")]
    public float floatSpeed = 2f;
    
    [Tooltip("Floating height")]
    public float floatHeight = 0.2f;
    
    [Tooltip("Rotation speed")]
    public float rotateSpeed = 90f;
    
    [Header("Audio")]
    public AudioClip pickupSound;
    [Range(0f, 1f)]
    public float pickupVolume = 0.8f;
    
    [Header("UI Feedback")]
    [Tooltip("Text to show when picked up")]
    public string pickupMessage = "STAMINA BOOST!";
    
    // Private
    private Vector3 startPosition;
    private bool isPickedUp = false;
    private MeshRenderer meshRenderer;
    private Collider pickupCollider;
    
    void Start()
    {
        startPosition = transform.position;
        meshRenderer = GetComponent<MeshRenderer>();
        pickupCollider = GetComponent<Collider>();
        
        // Make sure collider is trigger
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }
    
    void Update()
    {
        if (isPickedUp) return;
        
        // Floating animation
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        
        // Rotation animation
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;
        
        // Check if it's local player
        if (!other.CompareTag("Player")) return;
        
        PhotonView playerPV = other.GetComponent<PhotonView>();
        if (playerPV == null || !playerPV.IsMine) return;
        
        // Get player's stamina controller
        var controller = other.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller == null) return;
        
        Debug.Log("[Soda] Local player picked up soda!");
        
        // Apply boost to local player
        controller.ActivateStaminaBoost(boostDuration);
        
        // Notify all players to hide this soda
        photonView.RPC("RPC_PickupSoda", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_PickupSoda()
    {
        if (isPickedUp) return;
        isPickedUp = true;
        
        Debug.Log("[Soda] RPC_PickupSoda received - hiding soda for everyone");
        
        // Play pickup effect
        if (pickupEffect != null)
        {
            GameObject effect = Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        // Play sound (at position so all nearby players hear it)
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);
        }
        
        // Hide the soda (don't destroy - let master handle that)
        if (meshRenderer != null)
            meshRenderer.enabled = false;
        if (pickupCollider != null)
            pickupCollider.enabled = false;
        
        // Disable all child renderers too
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
        
        // Master client destroys the object after a short delay
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DestroyAfterDelay(0.5f));
        }
    }
    
    System.Collections.IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (photonView != null && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
    
    // ==================== RESPAWN (Optional) ====================
    
    /// <summary>
    /// Call this to respawn the soda (if you want it to respawn after some time)
    /// </summary>
    public void Respawn()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        photonView.RPC("RPC_RespawnSoda", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_RespawnSoda()
    {
        isPickedUp = false;
        
        if (meshRenderer != null)
            meshRenderer.enabled = true;
        if (pickupCollider != null)
            pickupCollider.enabled = true;
        
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = true;
        }
        
        Debug.Log("[Soda] Soda respawned!");
    }
    
    // ==================== DEBUG ====================
    
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}