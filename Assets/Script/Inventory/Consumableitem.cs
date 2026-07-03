using UnityEngine;
using Photon.Pun;

/// <summary>
/// Consumable Item - Extends Item class
/// Used for items like Soda that can be consumed for effects
/// Works with existing ItemPickup and InventorySystem
/// </summary>
public class ConsumableItem : Item
{
    [Header("=== CONSUMABLE SETTINGS ===")]
    [Tooltip("Type of consumable effect")]
    public ConsumableType consumableType = ConsumableType.StaminaBoost;
    
    [Tooltip("Duration of effect in seconds")]
    public float effectDuration = 10f;
    
    [Tooltip("Amount for instant effects (healing, etc)")]
    public float effectAmount = 100f;
    
    [Header("Visual Feedback")]
    [Tooltip("Particle effect when consumed")]
    public GameObject consumeEffect;
    
    [Header("Audio")]
    public AudioClip consumeSound;
    [Range(0f, 1f)]
    public float consumeVolume = 0.8f;
    
    [Header("Pickup Settings (World Item)")]
    [Tooltip("If true, item is consumed immediately on pickup instead of going to inventory")]
    public bool consumeOnPickup = false;
    
    // Private
    private bool isConsumed = false;
    
    void Start()
    {
        // Mark as consumable
        isConsumable = true;
        isThrowable = false;
        makesSound = false;
    }
    
    /// <summary>
    /// Override Use() from Item - called when player uses item from inventory
    /// </summary>
    public override void Use()
    {
        Debug.Log($"[Consumable] Using {itemName}");
        
        // Find local player and apply effect
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            ApplyEffect(localPlayer);
        }
        else
        {
            Debug.LogWarning("[Consumable] Could not find local player!");
        }
    }
    
    /// <summary>
    /// Called when player picks up world item (if consumeOnPickup = true)
    /// </summary>
    public void ConsumeOnPickup(GameObject player)
    {
        if (isConsumed) return;
        
        Debug.Log($"[Consumable] {itemName} consumed on pickup!");
        
        ApplyEffect(player);
        
        // Sync disappear to all players
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && PhotonNetwork.IsConnected)
        {
            pv.RPC("RPC_ConsumeItem", RpcTarget.All);
        }
        else
        {
            // Single player - just destroy
            PlayConsumeEffects();
            Destroy(gameObject, 0.1f);
        }
    }
    
    /// <summary>
    /// Apply the consumable effect to player
    /// </summary>
    void ApplyEffect(GameObject player)
    {
        switch (consumableType)
        {
            case ConsumableType.StaminaBoost:
                ApplyStaminaBoost(player);
                break;
                
            case ConsumableType.StaminaRefill:
                ApplyStaminaRefill(player);
                break;
                
            case ConsumableType.SpeedBoost:
                ApplySpeedBoost(player);
                break;
                
            // Add more effects here as needed
        }
        
        // Play consume effects locally
        PlayConsumeEffects();
    }
    
    void ApplyStaminaBoost(GameObject player)
    {
        var controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.ActivateStaminaBoost(effectDuration);
            Debug.Log($"[Consumable] Stamina boost activated for {effectDuration} seconds!");
        }
        else
        {
            Debug.LogWarning("[Consumable] ThirdPersonController not found on player!");
        }
    }
    
    void ApplyStaminaRefill(GameObject player)
    {
        var controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.CurrentStamina = controller.MaxStamina;
            Debug.Log("[Consumable] Stamina refilled!");
        }
    }
    
    void ApplySpeedBoost(GameObject player)
    {
        // Implement speed boost if needed
        Debug.Log($"[Consumable] Speed boost for {effectDuration} seconds!");
        // You can add SpeedBoost system similar to StaminaBoost
    }
    
    void PlayConsumeEffects()
    {
        // Play particle effect
        if (consumeEffect != null)
        {
            GameObject effect = Instantiate(consumeEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        // Play sound
        if (consumeSound != null)
        {
            AudioSource.PlayClipAtPoint(consumeSound, transform.position, consumeVolume);
        }
    }
    
    GameObject FindLocalPlayer()
    {
        // Find all players and return the local one
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                return player;
            }
        }
        
        // Fallback for single player
        if (players.Length > 0)
            return players[0];
            
        return null;
    }
    
    [PunRPC]
    void RPC_ConsumeItem()
    {
        if (isConsumed) return;
        isConsumed = true;
        
        Debug.Log($"[Consumable] RPC_ConsumeItem - {itemName} disappearing for all players");
        
        // Play effects
        PlayConsumeEffects();
        
        // Hide immediately
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        // Master destroys
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DestroyAfterDelay(0.5f));
        }
    }
    
    System.Collections.IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}

/// <summary>
/// Types of consumable effects
/// </summary>
public enum ConsumableType
{
    StaminaBoost,      // Unlimited stamina for duration
    StaminaRefill,     // Instant full stamina
    SpeedBoost,        // Faster movement for duration
    HealthRestore,     // Restore health (if you have health system)
    Invisibility       // Cannot be seen by NPCs for duration
}