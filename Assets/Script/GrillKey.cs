using UnityEngine;

/// <summary>
/// GrillKey - A key item that can unlock grill doors
/// You can change the Item Name in Inspector to create different keys
/// e.g., "Grill Key", "Grill Key 2", "Grill Key Floor 2"
/// </summary>
public class GrillKey : Item
{
    void Awake()
    {
        // ✅ FIXED: Only set default name if Inspector name is empty
        if (string.IsNullOrEmpty(itemName))
        {
            itemName = "Grill Key";
        }
        // Otherwise, keep whatever name is set in Inspector!
        
        // Set key behaviors
        isConsumable = true;
        isThrowable = false;
        makesSound = false;
        
        // Auto-assign prefab if not set
        if (itemPrefab == null)
        {
            #if UNITY_EDITOR
            GameObject prefabRoot = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefabRoot != null)
            {
                itemPrefab = prefabRoot;
            }
            else
            {
                itemPrefab = gameObject;
            }
            #endif
        }
        
        GameLog.Log($"Key initialized: '{itemName}' | Icon: {(icon != null ? "✅" : "❌")} | Prefab: {(itemPrefab != null ? "✅" : "❌")}");
    }
    
    public override void Use()
    {
        GameLog.Log($"Find the grill door that requires '{itemName}' to unlock it!");
    }
}