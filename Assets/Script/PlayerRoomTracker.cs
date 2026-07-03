using UnityEngine;

public class PlayerRoomTracker : MonoBehaviour
{
    public static int CurrentRoomID = 0;      // Current room player is in
    public static int CurrentFloorLevel = 0;  // Current floor level
    
    void OnTriggerEnter(Collider other)
    {
        RoomTrigger room = other.GetComponent<RoomTrigger>();
        if (room != null)
        {
            CurrentRoomID = room.roomID;
            CurrentFloorLevel = room.floorLevel;
            Debug.Log($"Player entered Room {CurrentRoomID}, Floor {CurrentFloorLevel}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        RoomTrigger room = other.GetComponent<RoomTrigger>();
        if (room != null && CurrentRoomID == room.roomID)
        {
            CurrentRoomID = 0;
            CurrentFloorLevel = 0;
            Debug.Log("Player exited the room");
        }
    }
    
    /// <summary>
    /// Reset static variables (call on level restart)
    /// </summary>
    public static void ResetStatic()
    {
        CurrentRoomID = 0;
        CurrentFloorLevel = 0;
    }
}