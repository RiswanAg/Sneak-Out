using UnityEngine;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Binds the scene camera to the LOCAL player only.
/// Attach this to your player prefab.
/// 
/// Works with:
/// - ThirdPersonCamera (CustomCharacterController)
/// - Cinemachine
/// </summary>
public class PunCameraBinder : MonoBehaviourPun
{
    [Header("Settings")]
    [Tooltip("Delay before binding camera (helps with scene load timing)")]
    public float bindDelay = 0.1f;
    
    void Start()
    {
        // ✅ CRITICAL: Only bind camera for LOCAL player
        if (!photonView.IsMine)
        {
            GameLog.Log($"[CameraBinder] {gameObject.name} is REMOTE player, skipping camera bind");
            return;
        }
        
        GameLog.Log($"[CameraBinder] {gameObject.name} is LOCAL player, binding camera...");
        StartCoroutine(BindCameraDelayed());
    }
    
    IEnumerator BindCameraDelayed()
    {
        // Wait for scene to fully load
        yield return new WaitForSeconds(bindDelay);
        
        BindCamera();
    }
    
    void BindCamera()
    {
        // ==================== TRY 1: Custom ThirdPersonCamera ====================
        // Using reflection to avoid namespace issues
        MonoBehaviour[] allCameras = FindObjectsOfType<MonoBehaviour>();
        foreach (var cam in allCameras)
        {
            string typeName = cam.GetType().Name;
            
            if (typeName == "ThirdPersonCamera")
            {
                // Try SetTarget method
                var setTargetMethod = cam.GetType().GetMethod("SetTarget");
                if (setTargetMethod != null)
                {
                    setTargetMethod.Invoke(cam, new object[] { transform });
                    GameLog.Log($"[CameraBinder] ✅ Bound ThirdPersonCamera via SetTarget to {gameObject.name}");
                    SetupController(cam.transform);
                    return;
                }
                
                // Try target field
                var targetField = cam.GetType().GetField("target");
                if (targetField != null)
                {
                    targetField.SetValue(cam, transform);
                    GameLog.Log($"[CameraBinder] ✅ Bound ThirdPersonCamera via target field to {gameObject.name}");
                    SetupController(cam.transform);
                    return;
                }
            }
        }
        
        // ==================== TRY 2: Cinemachine ====================
        #if CINEMACHINE
        var cinemachine = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
        if (cinemachine != null)
        {
            cinemachine.Follow = transform;
            cinemachine.LookAt = transform;
            GameLog.Log($"[CameraBinder] ✅ Bound Cinemachine to {gameObject.name}");
            return;
        }
        #endif
        
        // ==================== TRY 3: Any script with "target" field ====================
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            MonoBehaviour[] scripts = mainCam.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                var field = script.GetType().GetField("target");
                if (field != null && field.FieldType == typeof(Transform))
                {
                    field.SetValue(script, transform);
                    GameLog.Log($"[CameraBinder] ✅ Bound camera via {script.GetType().Name}.target to {gameObject.name}");
                    SetupController(mainCam.transform);
                    return;
                }
            }
        }
        
        Debug.LogWarning($"[CameraBinder] ⚠️ Could not find camera to bind for {gameObject.name}");
    }
    
    void SetupController(Transform cameraTransform)
    {
        // Set camera reference on controller if it needs it
        MonoBehaviour[] controllers = GetComponents<MonoBehaviour>();
        foreach (var ctrl in controllers)
        {
            string typeName = ctrl.GetType().Name;
            
            // Skip Photon components
            if (typeName.StartsWith("Photon")) continue;
            
            // Try to find cameraTransform field
            var cameraField = ctrl.GetType().GetField("cameraTransform");
            if (cameraField != null && cameraField.FieldType == typeof(Transform))
            {
                cameraField.SetValue(ctrl, cameraTransform);
                GameLog.Log($"[CameraBinder] Set cameraTransform on {typeName}");
                return;
            }
        }
    }
}