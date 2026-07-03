using UnityEngine;
using Photon.Pun;

/// <summary>
/// Throw Mechanic - Thrown items can be picked up again!
/// Press Q to throw the currently equipped item
/// ✅ FIXED: Now uses updated ThrownItemSound with room detection
/// </summary>
public class ThrowMechanic : MonoBehaviourPun
{
    [Header("References")]
    public InventorySystem inventory;
    public Transform throwPoint;
    
    [Header("Throw Settings")]
    public KeyCode throwKey = KeyCode.Q;
    public float throwForce = 15f;
    public float upwardForce = 2f;
    
    [Header("Physics Settings")]
    [Tooltip("Add colliders to thrown objects if missing")]
    public bool autoAddCollider = true;
    
    [Tooltip("Default collider size for objects without mesh")]
    public Vector3 defaultColliderSize = new Vector3(0.1f, 0.2f, 0.1f);
    
    [Tooltip("Mass of thrown objects")]
    public float thrownObjectMass = 0.5f;
    
    [Header("Pickup Settings")]
    [Tooltip("Make thrown items pickup-able again")]
    public bool canPickupThrownItems = true;
    
    [Tooltip("Layer for thrown items (optional)")]
    public string thrownItemLayer = "Default";
    
    [Header("Thrown Object Settings")]
    [Tooltip("Time before thrown items despawn (0 = never)")]
    public float despawnTime = 0f;

    void Start()
    {
        // Only allow local player to throw
        if (photonView != null && !photonView.IsMine)
        {
            enabled = false;
            return;
        }

        // Auto-find inventory
        if (inventory == null)
        {
            inventory = GetComponent<InventorySystem>();
        }

        // Auto-find throw point (use camera)
        if (throwPoint == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                throwPoint = cam.transform;
            }
        }

        GameLog.Log($"ThrowMechanic: Setup complete. Can pickup thrown items: {canPickupThrownItems}");
    }

    void Update()
    {
        if (Input.GetKeyDown(throwKey))
        {
            TryThrow();
        }
    }

    void TryThrow()
    {
        if (inventory == null || throwPoint == null)
        {
            Debug.LogWarning("ThrowMechanic: Missing inventory or throw point!");
            return;
        }

        ItemData equippedItem = inventory.GetEquippedItem();

        if (equippedItem == null)
        {
            GameLog.Log("No item equipped to throw!");
            return;
        }

        if (equippedItem.itemPrefab == null)
        {
            Debug.LogWarning($"Cannot throw {equippedItem.itemName} - no prefab!");
            return;
        }

        GameLog.Log($"🎯 Throwing: {equippedItem.itemName}");
        
        // Calculate spawn position and rotation
        Vector3 spawnPosition = throwPoint.position + throwPoint.forward * 0.5f;
        Quaternion spawnRotation = throwPoint.rotation;
        
        // Instantiate the thrown object
        GameObject thrownObject = Instantiate(equippedItem.itemPrefab, spawnPosition, spawnRotation);
        thrownObject.name = equippedItem.itemName;
        
        // ✅ SETUP PHYSICS
        SetupPhysics(thrownObject);
        
        // ✅ MAKE IT PICKUP-ABLE AGAIN
        if (canPickupThrownItems)
        {
            MakePickupable(thrownObject, equippedItem);
        }
        
        // Get rigidbody and apply throw force
        Rigidbody rb = thrownObject.GetComponent<Rigidbody>();
        
        Vector3 throwDirection = throwPoint.forward + (throwPoint.up * 0.1f);
        rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);
        rb.AddForce(throwPoint.up * upwardForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        
        // ✅ Add sound detection with room awareness
        thrownObject.AddComponent<ThrownItemSound>();
        
        // Set layer if specified
        if (!string.IsNullOrEmpty(thrownItemLayer))
        {
            int layer = LayerMask.NameToLayer(thrownItemLayer);
            if (layer >= 0)
            {
                thrownObject.layer = layer;
            }
        }
        
        // Remove from inventory
        inventory.RemoveEquippedItem();
        
        // Auto-despawn after time (if enabled)
        if (despawnTime > 0)
        {
            Destroy(thrownObject, despawnTime);
            GameLog.Log($"Item will despawn in {despawnTime} seconds");
        }

        GameLog.Log($"✅ {equippedItem.itemName} thrown and is now pickup-able!");
    }

    /// <summary>
    /// Setup proper physics for thrown objects
    /// </summary>
    void SetupPhysics(GameObject obj)
    {
        // ====== RIGIDBODY ======
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }
        
        // Configure rigidbody
        rb.mass = thrownObjectMass;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // ====== COLLIDER ======
        Collider existingCollider = obj.GetComponent<Collider>();
        
        if (existingCollider == null && autoAddCollider)
        {
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
                boxCollider.center = bounds.center;
                boxCollider.size = bounds.size;
                boxCollider.isTrigger = false;
            }
            else
            {
                BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
                boxCollider.size = defaultColliderSize;
                boxCollider.isTrigger = false;
            }
        }
        else if (existingCollider != null)
        {
            existingCollider.isTrigger = false;
            existingCollider.enabled = true;
        }
        
        // Fix child colliders too
        Collider[] childColliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in childColliders)
        {
            col.isTrigger = false;
        }
    }

    /// <summary>
    /// ✅ Make thrown object pickup-able again
    /// </summary>
    void MakePickupable(GameObject obj, ItemData itemData)
    {
        Item existingItem = obj.GetComponent<Item>();
        
        if (existingItem == null)
        {
            Item newItem = obj.AddComponent<Item>();
            newItem.itemName = itemData.itemName;
            newItem.icon = itemData.icon;
            newItem.itemPrefab = itemData.itemPrefab;
            
            GameLog.Log($"✅ Added Item component to {obj.name} - now pickup-able!");
        }
        else
        {
            existingItem.itemName = itemData.itemName;
            existingItem.icon = itemData.icon;
            existingItem.itemPrefab = itemData.itemPrefab;
            
            GameLog.Log($"✅ Updated existing Item component on {obj.name}");
        }
        
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }
}

// ==================== THROWN ITEM SOUND COMPONENT ====================

/// <summary>
/// Component added to thrown items to detect collisions and emit sounds
/// ✅ FIXED: Now detects which room the item landed in
/// </summary>
public class ThrownItemSound : MonoBehaviour
{
    private bool hasLanded = false;
    private float minVelocityForSound = 2f;
    private int itemRoomID = 0; // Room where this item is located

    void Start()
    {
        // Detect initial room when item is created
        DetectRoom();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        float impactVelocity = rb.linearVelocity.magnitude;

        if (impactVelocity < minVelocityForSound) return;

        hasLanded = true;

        // ✅ Detect room where item landed
        DetectRoom();

        if (SoundDetectionSystem.Instance != null)
        {
            SoundType soundType = SoundType.Medium;
            
            if (impactVelocity > 8f)
            {
                soundType = SoundType.Loud;
            }
            else if (impactVelocity > 5f)
            {
                soundType = SoundType.Medium;
            }
            else
            {
                soundType = SoundType.Quiet;
            }

            GameLog.Log($"🔊 Thrown item impact in Room {itemRoomID}: {soundType} (velocity: {impactVelocity:F1})");
            
            // ✅ Notify NPCs with explicit room ID
            NotifyNPCsInRoom(soundType);
        }
    }

    /// <summary>
    /// ✅ Detect which room this item is in
    /// </summary>
    void DetectRoom()
    {
        // Raycast down to find room trigger
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            RoomTrigger room = hit.collider.GetComponent<RoomTrigger>();
            if (room != null)
            {
                itemRoomID = room.roomID;
                GameLog.Log($"Thrown item detected in Room {itemRoomID}");
                return;
            }
        }

        // Fallback: Check for overlapping room triggers
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in colliders)
        {
            RoomTrigger room = col.GetComponent<RoomTrigger>();
            if (room != null)
            {
                itemRoomID = room.roomID;
                GameLog.Log($"Thrown item found in Room {itemRoomID} via overlap");
                return;
            }
        }

        Debug.LogWarning($"Thrown item couldn't detect room - defaulting to Room 0");
        itemRoomID = 0;
    }

    /// <summary>
    /// ✅ Notify all NPCs in the same room
    /// </summary>
    void NotifyNPCsInRoom(SoundType soundType)
    {
        // Find all StudentNPC objects
        StudentNPC[] allStudents = FindObjectsOfType<StudentNPC>();

        foreach (StudentNPC student in allStudents)
        {
            // Check if student has the method (they should)
            if (student != null)
            {
                // Call the new method with explicit room ID
                student.OnSoundHeardWithRoom(transform.position, soundType, 0f, itemRoomID, gameObject);
            }
        }

        GameLog.Log($"Notified {allStudents.Length} students about sound in Room {itemRoomID}");
    }
}