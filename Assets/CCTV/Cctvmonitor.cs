using UnityEngine;

// SIMPLE MONITOR - TAKDE PHOTON, GUNA INTERACTION BIASA
public class CCTVMonitor : MonoBehaviour
{
    [Header("Monitor Settings")]
    [Tooltip("The screen material/renderer that will display the feed")]
    public Renderer monitorScreen;
    
    [Tooltip("Material index if the monitor has multiple materials")]
    public int materialIndex = 0;
    
    [Header("Camera Feed")]
    [Tooltip("Which CCTV camera to display on this monitor")]
    public CCTVCamera assignedCamera;
    
    [Tooltip("Allow switching between cameras with E key")]
    public bool allowCameraSwitching = false;
    
    [Tooltip("All available cameras to cycle through")]
    public CCTVCamera[] availableCameras;
    
    [Header("Interaction")]
    [Tooltip("Interaction key")]
    public KeyCode interactKey = KeyCode.E;
    
    [Header("Visual Effects (Optional)")]
    [Tooltip("Static/noise texture when no camera assigned")]
    public Texture staticTexture;
    
    [Tooltip("Monitor power light (green when on)")]
    public Light powerLight;
    
    private bool playerNear = false;
    private int currentCameraIndex = 0;
    private Material screenMaterial;
    
    void Start()
    {
        // Get screen material
        if (monitorScreen != null)
        {
            Material[] mats = monitorScreen.materials;
            if (materialIndex < mats.Length)
            {
                screenMaterial = mats[materialIndex];
            }
            
            // Set initial camera feed
            if (assignedCamera != null)
            {
                ShowCameraFeed(assignedCamera);
            }
            else if (availableCameras != null && availableCameras.Length > 0)
            {
                ShowCameraFeed(availableCameras[0]);
            }
            else
            {
                ShowStatic();
            }
        }
        
        // Setup power light
        if (powerLight != null)
        {
            powerLight.color = Color.green;
            powerLight.intensity = 1f;
            powerLight.enabled = true;
        }
    }
    
    void Update()
    {
        // Switch cameras when player presses E
        if (playerNear && allowCameraSwitching && Input.GetKeyDown(interactKey))
        {
            SwitchCamera();
        }
    }
    
    // Show camera feed on this monitor
    public void ShowCameraFeed(CCTVCamera camera)
    {
        if (camera == null || screenMaterial == null) return;
        
        assignedCamera = camera;
        RenderTexture cameraTexture = camera.GetTexture();
        
        if (cameraTexture != null)
        {
            screenMaterial.mainTexture = cameraTexture;
            GameLog.Log($"Monitor '{gameObject.name}' showing feed from '{camera.gameObject.name}'");
        }
        else
        {
            Debug.LogWarning($"Camera '{camera.gameObject.name}' takde RenderTexture!");
        }
    }
    
    // Switch to next camera
    void SwitchCamera()
    {
        if (availableCameras == null || availableCameras.Length == 0) return;
        
        currentCameraIndex = (currentCameraIndex + 1) % availableCameras.Length;
        
        CCTVCamera nextCamera = availableCameras[currentCameraIndex];
        if (nextCamera != null)
        {
            ShowCameraFeed(nextCamera);
        }
    }
    
    // Show static/noise when no feed
    void ShowStatic()
    {
        if (screenMaterial == null) return;
        
        if (staticTexture != null)
        {
            screenMaterial.mainTexture = staticTexture;
        }
        else
        {
            screenMaterial.color = Color.grey;
        }
    }
    
    // Turn monitor on/off
    public void SetPower(bool isOn)
    {
        if (monitorScreen != null)
        {
            monitorScreen.enabled = isOn;
        }
        
        if (powerLight != null)
        {
            powerLight.enabled = isOn;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
        }
    }
}