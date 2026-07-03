using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// FIXED VERSION - Properly handles retry/respawn scenarios
/// Destroys old player and spawns fresh one on scene reload
/// </summary>
public class PlayerSpawnerPun : MonoBehaviour
{
    [Header("Spawn points in the scene")]
    [Tooltip("Spawn point for Hazim (First player - ActorNumber 1)")]
    public Transform hazimDormSpawn;
    
    [Tooltip("Spawn point for Amir (Second player - ActorNumber 2)")]
    public Transform amirDormSpawn;

    [Header("Prefab names (must be in Assets/Resources/)")]
    [Tooltip("Must match exact prefab name in Resources folder!")]
    public string hazimPrefabName = "Hazim Player";
    public string amirPrefabName  = "Amir Player";

    void Start()
    {
        GameLog.Log("=== PlayerSpawnerPun Start ===");
        
        // ==================== VALIDATION CHECKS ====================
        
        // Check 1: Are we in a Photon room?
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("❌ NOT IN PHOTON ROOM!");
            Debug.LogError($"Network State: {PhotonNetwork.NetworkClientState}");
            return;
        }

        GameLog.Log($"✅ In room: {PhotonNetwork.CurrentRoom.Name}");
        GameLog.Log($"Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
        GameLog.Log($"My ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
        GameLog.Log($"IsMasterClient: {PhotonNetwork.IsMasterClient}");
        
        // ✅ FIXED: Always destroy old player and clear TagObject on scene load
        // This ensures fresh spawn on retry/reload
        CleanupOldPlayer();

        // Check 3: Find spawn points
        FindSpawnPoints();
        
        if (hazimDormSpawn == null || amirDormSpawn == null)
        {
            Debug.LogError("❌ SPAWN POINTS NOT FOUND!");
            Debug.LogError($"Hazim spawn: {(hazimDormSpawn != null ? "OK" : "MISSING")}");
            Debug.LogError($"Amir spawn: {(amirDormSpawn != null ? "OK" : "MISSING")}");
            return;
        }

        GameLog.Log("✅ Spawn points found");
        
        // ==================== CHARACTER ASSIGNMENT ====================
        
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        string character = (actorNumber == 1) ? "Hazim" : "Amir";
        
        // Save to custom properties for reference by other scripts
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "character", character } });
        
        GameLog.Log($"✅ Character assigned: {character} (ActorNumber: {actorNumber})");

        // ==================== SPAWN PLAYER ====================
        
        Transform spawn = (character == "Hazim") ? hazimDormSpawn : amirDormSpawn;
        string prefab   = (character == "Hazim") ? hazimPrefabName : amirPrefabName;

        GameLog.Log($"Spawning prefab '{prefab}' at position {spawn.position}");

        try
        {
            // Spawn player across network
            GameObject player = PhotonNetwork.Instantiate(prefab, spawn.position, spawn.rotation);
            
            if (player != null)
            {
                // Tag this player object for reference
                PhotonNetwork.LocalPlayer.TagObject = player;
                
                GameLog.Log($"✅✅✅ SUCCESS! Player spawned as {character}! ✅✅✅");
            }
            else
            {
                Debug.LogError("❌ PhotonNetwork.Instantiate returned null!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ EXCEPTION during spawn: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// ✅ FIXED: Cleanup any old player from previous scene/retry
    /// </summary>
    void CleanupOldPlayer()
    {
        GameLog.Log("[PlayerSpawner] Checking for old player to cleanup...");
        
        // Check if TagObject has an old player reference
        if (PhotonNetwork.LocalPlayer.TagObject != null)
        {
            GameObject oldPlayer = PhotonNetwork.LocalPlayer.TagObject as GameObject;
            
            if (oldPlayer != null)
            {
                GameLog.Log($"[PlayerSpawner] 🧹 Found old player: {oldPlayer.name}");
                
                // ✅ FIXED: Use regular Destroy instead of PhotonNetwork.Destroy
                // PhotonNetwork.Destroy causes errors during scene reload
                // Regular Destroy works fine since scene is reloading anyway
                Destroy(oldPlayer);
            }
            else
            {
                GameLog.Log("[PlayerSpawner] TagObject was stale (already destroyed)");
            }
            
            // Clear the reference
            PhotonNetwork.LocalPlayer.TagObject = null;
        }
        
        // ✅ EXTRA: Find and destroy any orphaned local players
        DestroyOrphanedPlayers();
    }
    
    /// <summary>
    /// Find and destroy any player objects that belong to us but aren't tracked
    /// </summary>
    void DestroyOrphanedPlayers()
    {
        // Find all PhotonViews that belong to local player
        PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        
        foreach (PhotonView pv in allViews)
        {
            if (pv == null) continue;
            if (!pv.IsMine) continue;
            
            // Check if this is a player object (has CharacterController or is tagged Player)
            if (pv.gameObject.CompareTag("Player") || 
                pv.GetComponent<CharacterController>() != null)
            {
                GameLog.Log($"[PlayerSpawner] 🧹 Found orphaned player, destroying: {pv.gameObject.name}");
                // ✅ FIXED: Use regular Destroy instead of PhotonNetwork.Destroy
                Destroy(pv.gameObject);
            }
        }
    }
    
    /// <summary>
    /// Auto-find spawn points if not assigned in Inspector
    /// </summary>
    void FindSpawnPoints()
    {
        if (hazimDormSpawn == null)
        {
            GameObject t = GameObject.Find("Spawn_HazimDorm");
            if (t != null)
            {
                hazimDormSpawn = t.transform;
                GameLog.Log("Auto-found Spawn_HazimDorm");
            }
            else
            {
                Debug.LogWarning("Could not find GameObject named 'Spawn_HazimDorm'");
            }
        }

        if (amirDormSpawn == null)
        {
            GameObject t = GameObject.Find("Spawn_AmirDorm");
            if (t != null)
            {
                amirDormSpawn = t.transform;
                GameLog.Log("Auto-found Spawn_AmirDorm");
            }
            else
            {
                Debug.LogWarning("Could not find GameObject named 'Spawn_AmirDorm'");
            }
        }
    }
    
    // Visualize spawn points in Scene view
    void OnDrawGizmos()
    {
        // Draw Hazim spawn (Blue)
        if (hazimDormSpawn != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(hazimDormSpawn.position, 0.5f);
            Gizmos.DrawLine(hazimDormSpawn.position, hazimDormSpawn.position + Vector3.up * 2f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(hazimDormSpawn.position + Vector3.up * 2.5f, "Hazim Spawn");
            #endif
        }
        
        // Draw Amir spawn (Red)
        if (amirDormSpawn != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(amirDormSpawn.position, 0.5f);
            Gizmos.DrawLine(amirDormSpawn.position, amirDormSpawn.position + Vector3.up * 2f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(amirDormSpawn.position + Vector3.up * 2.5f, "Amir Spawn");
            #endif
        }
    }
}