using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    public int roomID;
    public int floorLevel = 0;  // 0 = Ground floor, 1 = First floor, etc.
    
    void OnDrawGizmos()
    {
        // Visualize room boundaries in Scene view
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}