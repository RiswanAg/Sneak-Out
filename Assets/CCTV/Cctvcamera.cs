using UnityEngine;

// SIMPLE CCTV CAMERA - TAKDE PHOTON, MUDAH SAJA
public class CCTVCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The camera that will render the CCTV view")]
    public Camera cctvCamera;
    
    [Tooltip("RenderTexture for this CCTV camera - CREATE THIS IN PROJECT FOLDER!")]
    public RenderTexture renderTexture;
    
    [Header("Camera Properties")]
    [Tooltip("Field of view for the CCTV camera")]
    public float fieldOfView = 60f;
    
    [Header("Camera Rotation (Optional)")]
    [Tooltip("Enable automatic rotation/patrol")]
    public bool autoRotate = false;
    
    [Tooltip("Rotation speed if auto-rotate is enabled")]
    public float rotationSpeed = 10f;
    
    [Tooltip("Rotation range (left to right in degrees)")]
    public float rotationRange = 60f;
    
    [Header("Visual Effects (Optional)")]
    [Tooltip("Red indicator light - drag Point Light here")]
    public Light indicatorLight;
    
    [Tooltip("Blink interval for indicator")]
    public float blinkInterval = 1f;
    
    private bool rotatingRight = true;
    private float blinkTimer = 0f;
    private float initialYRotation;
    
    void Start()
    {
        // SETUP CAMERA
        if (cctvCamera != null)
        {
            cctvCamera.fieldOfView = fieldOfView;
            cctvCamera.enabled = true;
            
            // Assign RenderTexture if available
            if (renderTexture != null)
            {
                cctvCamera.targetTexture = renderTexture;
            }
            else
            {
                Debug.LogWarning($"CCTV '{gameObject.name}' takde RenderTexture! Kena assign manually!");
            }
        }
        
        // Setup indicator light
        if (indicatorLight != null)
        {
            indicatorLight.color = Color.red;
            indicatorLight.intensity = 2f;
            indicatorLight.range = 0.5f;
        }
        
        // Store initial rotation for auto-rotate
        initialYRotation = transform.localEulerAngles.y;
        
        GameLog.Log($"CCTV Camera '{gameObject.name}' ready!");
    }
    
    void Update()
    {
        // Auto-rotation (patrol mode)
        if (autoRotate)
        {
            RotateCamera();
        }
        
        // Blink indicator light
        if (indicatorLight != null)
        {
            BlinkIndicator();
        }
    }
    
    void RotateCamera()
    {
        float currentY = transform.localEulerAngles.y;
        float minAngle = initialYRotation - rotationRange / 2f;
        float maxAngle = initialYRotation + rotationRange / 2f;
        
        if (rotatingRight)
        {
            currentY += rotationSpeed * Time.deltaTime;
            if (currentY >= maxAngle)
            {
                currentY = maxAngle;
                rotatingRight = false;
            }
        }
        else
        {
            currentY -= rotationSpeed * Time.deltaTime;
            if (currentY <= minAngle)
            {
                currentY = minAngle;
                rotatingRight = true;
            }
        }
        
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, currentY, transform.localEulerAngles.z);
    }
    
    void BlinkIndicator()
    {
        blinkTimer += Time.deltaTime;
        
        if (blinkTimer >= blinkInterval)
        {
            indicatorLight.enabled = !indicatorLight.enabled;
            blinkTimer = 0f;
        }
    }
    
    // Turn camera on/off
    public void SetActive(bool active)
    {
        if (cctvCamera != null)
            cctvCamera.enabled = active;
        
        if (indicatorLight != null)
            indicatorLight.enabled = active;
    }
    
    // Get the RenderTexture (for monitors to use)
    public RenderTexture GetTexture()
    {
        return renderTexture;
    }
}