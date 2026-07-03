using UnityEngine;
using Photon.Pun;

/// <summary>
/// ItemPickup - With duplicate pickup prevention and consumable support
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public Transform playerCamera;
    public float rayLength = 3f;
    public LayerMask pickupMask;
    
    [Header("References")]
    public InventorySystem inventory;
    
    [Header("Visual Feedback")]
    public bool showPickupPrompt = true;
    
    [Header("Debug Info (Read Only)")]
    public bool isLocalPlayer = false;
    public bool cameraFound = false;
    public bool inventoryFound = false;
    public string lookingAtItem = "Nothing";
    
    private Item currentLookingAtItem = null;
    
    // Anti-duplicate cooldown
    private float pickupCooldown = 0f;
    private const float PICKUP_COOLDOWN_TIME = 0.3f;

    void Start()
    {
        Debug.Log("=== ItemPickup: Start() called ===");

        // Check if this is a networked player
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null)
        {
            isLocalPlayer = pv.IsMine;
            Debug.Log($"ItemPickup: PhotonView found. IsMine = {isLocalPlayer}");
            
            if (!isLocalPlayer)
            {
                Debug.Log("ItemPickup: Not local player, disabling");
                enabled = false;
                return;
            }
        }
        else
        {
            Debug.Log("ItemPickup: No PhotonView (single player mode)");
            isLocalPlayer = true;
        }

        // Find camera
        if (playerCamera == null)
        {
            Debug.Log("ItemPickup: Camera not assigned, searching...");
            playerCamera = Camera.main?.transform;
            
            if (playerCamera != null)
            {
                Debug.Log($"ItemPickup: Found camera: {playerCamera.name}");
            }
            else
            {
                Debug.LogError("ItemPickup: NO CAMERA FOUND!");
            }
        }
        else
        {
            Debug.Log($"ItemPickup: Camera already assigned: {playerCamera.name}");
        }

        cameraFound = (playerCamera != null);

        // Find inventory
        if (inventory == null)
        {
            Debug.Log("ItemPickup: Inventory not assigned, searching...");
            inventory = GetComponent<InventorySystem>();
            
            if (inventory != null)
            {
                Debug.Log("ItemPickup: Found InventorySystem on this GameObject");
            }
            else
            {
                Debug.LogError("ItemPickup: NO INVENTORY FOUND!");
            }
        }
        else
        {
            Debug.Log("ItemPickup: Inventory already assigned");
        }

        inventoryFound = (inventory != null);

        Debug.Log("=== ItemPickup Setup Complete ===");
    }

    void Update()
    {
        if (!isLocalPlayer || playerCamera == null) return;

        // Update cooldown
        if (pickupCooldown > 0f)
        {
            pickupCooldown -= Time.deltaTime;
        }

        // Raycast to detect items
        RaycastHit hit;
        bool hitSomething;
        
        if (pickupMask.value != 0)
        {
            hitSomething = Physics.Raycast(
                playerCamera.position, 
                playerCamera.forward, 
                out hit, 
                rayLength,
                pickupMask
            );
        }
        else
        {
            hitSomething = Physics.Raycast(
                playerCamera.position, 
                playerCamera.forward, 
                out hit, 
                rayLength
            );
        }

        if (hitSomething)
        {
            Item item = FindItemComponent(hit.collider.gameObject);
            
            if (item != null)
            {
                currentLookingAtItem = item;
                lookingAtItem = item.itemName;
                
                if (Input.GetMouseButtonDown(0) && pickupCooldown <= 0f)
                {
                    Debug.Log("=== LEFT MOUSE CLICKED ===");
                    TryPickupItem(item);
                    pickupCooldown = PICKUP_COOLDOWN_TIME;
                }
            }
            else
            {
                currentLookingAtItem = null;
                lookingAtItem = $"Object: {hit.collider.gameObject.name} (No Item component)";
            }
        }
        else
        {
            currentLookingAtItem = null;
            lookingAtItem = "Nothing";
        }
    }

    void TryPickupItem(Item item)
    {
        if (item == null || item.gameObject == null)
        {
            Debug.LogWarning("Item already picked up or destroyed!");
            return;
        }
        
        Debug.Log($"TryPickupItem() called for: {item.itemName}");

        // ✅ CHECK: Is this a consumable that should be consumed on pickup?
        ConsumableItem consumable = item as ConsumableItem;
        if (consumable == null)
        {
            // Try GetComponent in case it's not directly cast
            consumable = item.GetComponent<ConsumableItem>();
        }
        
        if (consumable != null && consumable.consumeOnPickup)
        {
            Debug.Log($"[ItemPickup] {item.itemName} is consumable - consuming on pickup!");
            
            // Disable collider immediately
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null)
            {
                itemCollider.enabled = false;
            }
            
            // Consume immediately instead of adding to inventory
            consumable.ConsumeOnPickup(gameObject);
            
            currentLookingAtItem = null;
            lookingAtItem = "Nothing";
            return;
        }

        // Normal item - add to inventory
        if (inventory == null)
        {
            Debug.LogError("FAIL: No inventory!");
            inventory = GetComponent<InventorySystem>();
            
            if (inventory == null)
            {
                Debug.LogError("STILL NO INVENTORY FOUND!");
                return;
            }
        }

        // Disable collider immediately to prevent re-detection
        Collider col = item.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        bool success = inventory.AddItem(item);

        if (success)
        {
            Debug.Log($"SUCCESS! Picked up: {item.itemName}");
            currentLookingAtItem = null;
            lookingAtItem = "Nothing";
        }
        else
        {
            Debug.LogWarning($"FAILED to pick up {item.itemName}");
            
            // Re-enable collider if pickup failed
            if (col != null)
            {
                col.enabled = true;
            }
        }
    }

    void OnGUI()
    {
        if (!showPickupPrompt || currentLookingAtItem == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        // ✅ Different prompt for consumables
        string action = "pick up";
        ConsumableItem consumable = currentLookingAtItem as ConsumableItem;
        if (consumable == null)
        {
            consumable = currentLookingAtItem.GetComponent<ConsumableItem>();
        }
        
        if (consumable != null && consumable.consumeOnPickup)
        {
            action = "consume";
        }
        
        string promptText = $"Left Click to {action} {currentLookingAtItem.itemName}";

        // Shadow
        GUI.contentColor = Color.black;
        GUI.Label(new Rect(Screen.width / 2 - 149, Screen.height / 2 + 51, 300, 30), 
            promptText, style);
        
        // Main text
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), 
            promptText, style);
    }

    void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Gizmos.color = currentLookingAtItem != null ? Color.green : Color.red;
        Gizmos.DrawRay(playerCamera.position, playerCamera.forward * rayLength);

        if (currentLookingAtItem != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentLookingAtItem.transform.position, 0.2f);
        }
    }

    Item FindItemComponent(GameObject hitObject)
    {
        // 1. Check the hit object itself
        Item item = hitObject.GetComponent<Item>();
        if (item != null) return item;

        // 2. Check parent objects
        Transform parent = hitObject.transform.parent;
        while (parent != null)
        {
            item = parent.GetComponent<Item>();
            if (item != null) return item;
            parent = parent.parent;
        }

        // 3. Check root object
        Transform root = hitObject.transform.root;
        if (root != hitObject.transform)
        {
            item = root.GetComponent<Item>();
            if (item != null) return item;
        }

        // 4. Check children
        item = hitObject.GetComponentInChildren<Item>();
        if (item != null) return item;

        return null;
    }
}