using UnityEngine;

/// <summary>
/// TriggerTest.cs - Simple test to check if trigger detection works
/// Add this to Manual_Pickup_Parent alongside ManualItem
/// Check Console for messages when anything enters the trigger
/// </summary>
public class TriggerTest : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TriggerTest] ENTER - Object: {other.gameObject.name}, Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
    }
    
    void OnTriggerStay(Collider other)
    {
        // Uncomment below if OnTriggerEnter doesn't fire
        // Debug.Log($"[TriggerTest] STAY - Object: {other.gameObject.name}");
    }
    
    void OnTriggerExit(Collider other)
    {
        Debug.Log($"[TriggerTest] EXIT - Object: {other.gameObject.name}");
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[TriggerTest] COLLISION (not trigger!) - Object: {collision.gameObject.name}");
    }
}