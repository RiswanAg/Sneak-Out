using UnityEngine;

/// <summary>
/// ItemData - Data container for inventory items
/// Extended to support Manual item type
/// </summary>
[System.Serializable]
public class ItemData
{
    public string itemName;
    public Sprite icon;
    public GameObject itemPrefab;
    
    // Consumable data
    public bool isConsumable = false;
    public ConsumableType consumableType;
    public float effectDuration;
    public float effectAmount;
    
    // ✅ NEW: Manual data
    public bool isManual = false;
    
    public ItemData(string name, Sprite iconSprite, GameObject prefab)
    {
        itemName = name;
        icon = iconSprite;
        itemPrefab = prefab;
        isConsumable = false;
        isManual = false;
    }
}

/// <summary>
/// Item - Base class for all pickupable items
/// </summary>
public class Item : MonoBehaviour
{
    [Header("Item Properties")]
    public string itemName;
    public Sprite icon;
    public GameObject itemPrefab;
    
    [Header("Optional: World Model")]
    [Tooltip("Leave empty if this GameObject itself is the world model")]
    public GameObject worldModel;

    [Header("Item Behavior Settings")]
    [Tooltip("Can this item be consumed/used up?")]
    public bool isConsumable = false;
    
    [Tooltip("Can this item be thrown?")]
    public bool isThrowable = true;
    
    [Tooltip("Does this item make sound when thrown?")]
    public bool makesSound = true;
    
    [Header("Sound Settings")]
    [Tooltip("Minimum sound level when item lands")]
    public SoundType minimumSoundType = SoundType.Loud;
    
    [Header("Audio (Optional)")]
    public AudioClip landingSound;
    
    [Range(0f, 1f)]
    public float landingVolume = 0.8f;
    
    // Private
    private AudioSource audioSource;
    private bool hasLanded = false;

    void Awake()
    {
        SetupAudio();
    }
    
    void SetupAudio()
    {
        if (landingSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.clip = landingSound;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 20f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = landingVolume;
        }
    }

    public virtual ItemData GetItemData()
    {
        ItemData data = new ItemData(itemName, icon, itemPrefab);
        data.isConsumable = isConsumable;
        return data;
    }

    public virtual void Use()
    {
        Debug.Log("Using item: " + itemName);
    }

    void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        
        if (rb != null && rb.linearVelocity.magnitude > 1f && makesSound && !hasLanded)
        {
            hasLanded = true;
            PlayLandingAudio();
            EmitSoundForNPCs();
        }
    }
    
    void PlayLandingAudio()
    {
        if (landingSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 20f;
            }
            
            audioSource.PlayOneShot(landingSound, landingVolume);
            Debug.Log($"🔊 Playing landing sound for {itemName}");
        }
    }
    
    /// <summary>
    /// Emit sound for NPCs to detect
    /// ✅ FIXED: Uses new EmitSound method (room detection automatic via PlayerRoomTracker)
    /// </summary>
    void EmitSoundForNPCs()
    {
        if (SoundDetectionSystem.Instance == null) return;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        SoundType soundType = minimumSoundType;
        
        // Louder sound if thrown hard
        if (rb != null && rb.linearVelocity.magnitude > 8f)
        {
            soundType = SoundType.Loud;
        }
        
        Debug.Log($"🔊 {itemName} landed and made {soundType} sound!");
        
        // ✅ FIXED: Use new EmitSound method
        // Room detection is now handled automatically by PlayerRoomTracker
        SoundDetectionSystem.Instance.EmitSound(
            transform.position, 
            soundType, 
            gameObject
        );
    }

    void OnValidate()
    {
        if (itemPrefab == null)
        {
            #if UNITY_EDITOR
            GameObject prefabRoot = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefabRoot != null)
                itemPrefab = prefabRoot;
            else
                itemPrefab = gameObject;
            #endif
        }
    }
}