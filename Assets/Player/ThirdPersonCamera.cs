using UnityEngine;

namespace CustomCharacterController
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        public Transform target;
        public float heightOffset = 1.4f;
        public float pivotOffset = 0f;
        
        [Header("Camera Distance")]
        public float defaultDistance = 2.5f;
        public float rightOffset = 0f;
        public float smoothFollow = 10f;
        
        [Header("Camera Rotation")]
        public float smoothCameraRotation = 12f;
        public float xMouseSensitivity = 3f;
        public float yMouseSensitivity = 3f;
        public float yMinLimit = -40f;
        public float yMaxLimit = 80f;
        
        [Header("Collision Detection")]
        public LayerMask cullingLayer = 1 << 0;
        public float checkHeightRadius = 0.4f;
        public float cullingHeight = 0.2f;
        public float cullingMinDist = 0.1f;
        public float clipPlaneMargin = 0f;
        
        [Header("Debug")]
        public bool lockCamera = false;
        
        // Private variables
        private Camera cam;
        private Transform targetLookAt;
        private Vector3 currentTargetPos;
        private Vector3 currentCameraPos;
        private Vector3 desiredCameraPos;
        private float distance;
        private float cullingDistance;
        private float currentHeight;
        private float mouseX = 0f;
        private float mouseY = 0f;
        private float xMinLimit = -360f;
        private float xMaxLimit = 360f;
        
        void Start()
        {
            Initialize();
        }
        
        void Initialize()
        {
            if (target == null)
            {
                Debug.LogError("ThirdPersonCamera: No target assigned!");
                return;
            }
            
            cam = GetComponent<Camera>();
            
            // Create look at target
            GameObject lookAtObj = new GameObject("CameraLookAt");
            targetLookAt = lookAtObj.transform;
            targetLookAt.position = target.position;
            targetLookAt.rotation = target.rotation;
            targetLookAt.SetParent(null);
            
            // Initialize rotation
            mouseX = target.eulerAngles.y;
            mouseY = target.eulerAngles.x;
            
            distance = defaultDistance;
            currentHeight = heightOffset;
        }
        
        void LateUpdate()
        {
            if (target == null || targetLookAt == null) return;
            
            UpdateCameraPosition();
        }
        
        public void RotateCamera(float x, float y)
        {
            if (lockCamera) return;
            
            mouseX += x * xMouseSensitivity;
            mouseY -= y * yMouseSensitivity;
            
            mouseY = ClampAngle(mouseY, yMinLimit, yMaxLimit);
            mouseX = ClampAngle(mouseX, xMinLimit, xMaxLimit);
        }
        
        void UpdateCameraPosition()
        {
            // Smooth distance
            distance = Mathf.Lerp(distance, defaultDistance, smoothFollow * Time.deltaTime);
            cullingDistance = Mathf.Lerp(cullingDistance, distance, Time.deltaTime);
            
            // Calculate target position
            Vector3 targetPos = new Vector3(target.position.x, target.position.y + pivotOffset, target.position.z);
            currentTargetPos = targetPos;
            
            // Calculate desired positions
            desiredCameraPos = targetPos + new Vector3(0, heightOffset, 0);
            currentCameraPos = currentTargetPos + new Vector3(0, currentHeight, 0);
            
            // Update look at target rotation
            Quaternion newRotation = Quaternion.Euler(mouseY, mouseX, 0);
            targetLookAt.rotation = Quaternion.Slerp(targetLookAt.rotation, newRotation, smoothCameraRotation * Time.deltaTime);
            targetLookAt.position = currentCameraPos;
            
            // Calculate camera direction
            Vector3 camDir = (-targetLookAt.forward) + (rightOffset * targetLookAt.right);
            camDir = camDir.normalized;
            
            // Handle collision detection
            RaycastHit hitInfo;
            
            // Check height collision
            if (Physics.SphereCast(targetPos, checkHeightRadius, Vector3.up, out hitInfo, cullingHeight + 0.2f, cullingLayer))
            {
                float t = hitInfo.distance - 0.2f;
                t -= heightOffset;
                t /= (cullingHeight - heightOffset);
                cullingHeight = Mathf.Lerp(heightOffset, cullingHeight, Mathf.Clamp(t, 0f, 1f));
            }
            
            // Check camera position collision
            ClipPlanePoints nearClipPoints = GetNearClipPlanePoints(currentCameraPos + (camDir * distance), clipPlaneMargin);
            
            if (CheckCameraCollision(currentCameraPos, nearClipPoints, out hitInfo, distance, cullingLayer))
            {
                distance = Mathf.Clamp(hitInfo.distance - 0.2f, cullingMinDist, defaultDistance);
                
                if (distance < defaultDistance)
                {
                    float t = hitInfo.distance - cullingMinDist;
                    t /= cullingMinDist;
                    currentHeight = Mathf.Lerp(cullingHeight, heightOffset, Mathf.Clamp(t, 0f, 1f));
                    currentCameraPos = currentTargetPos + new Vector3(0, currentHeight, 0);
                }
            }
            else
            {
                currentHeight = heightOffset;
            }
            
            // Final camera position
            Vector3 finalPosition = currentCameraPos + (camDir * distance);
            transform.position = finalPosition;
            
            // Look at target
            Vector3 lookPoint = currentCameraPos + targetLookAt.forward * 2f;
            lookPoint += (targetLookAt.right * Vector3.Dot(camDir * distance, targetLookAt.right));
            
            Quaternion lookRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = lookRotation;
        }
        
        bool CheckCameraCollision(Vector3 from, ClipPlanePoints clipPoints, out RaycastHit hitInfo, float distance, LayerMask layer)
        {
            bool hasHit = false;
            hitInfo = new RaycastHit();
            float closestDistance = distance;
            
            RaycastHit hit;
            
            // Check all four corners of near clip plane
            if (Physics.Raycast(from, clipPoints.lowerLeft - from, out hit, distance, layer))
            {
                hasHit = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitInfo = hit;
                }
            }
            
            if (Physics.Raycast(from, clipPoints.lowerRight - from, out hit, distance, layer))
            {
                hasHit = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitInfo = hit;
                }
            }
            
            if (Physics.Raycast(from, clipPoints.upperLeft - from, out hit, distance, layer))
            {
                hasHit = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitInfo = hit;
                }
            }
            
            if (Physics.Raycast(from, clipPoints.upperRight - from, out hit, distance, layer))
            {
                hasHit = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitInfo = hit;
                }
            }
            
            return hasHit && hitInfo.collider != null;
        }
        
        ClipPlanePoints GetNearClipPlanePoints(Vector3 position, float margin)
        {
            ClipPlanePoints points = new ClipPlanePoints();
            
            if (cam == null) return points;
            
            Transform camTransform = cam.transform;
            float halfFOV = (cam.fieldOfView / 2) * Mathf.Deg2Rad;
            float aspect = cam.aspect;
            float distance = cam.nearClipPlane;
            float height = distance * Mathf.Tan(halfFOV);
            float width = height * aspect;
            
            points.lowerRight = position + camTransform.right * (width + margin) - camTransform.up * (height + margin) + camTransform.forward * distance;
            points.lowerLeft = position - camTransform.right * (width + margin) - camTransform.up * (height + margin) + camTransform.forward * distance;
            points.upperRight = position + camTransform.right * (width + margin) + camTransform.up * (height + margin) + camTransform.forward * distance;
            points.upperLeft = position - camTransform.right * (width + margin) + camTransform.up * (height + margin) + camTransform.forward * distance;
            
            return points;
        }
        
        float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }
        
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (targetLookAt != null)
            {
                mouseX = target.eulerAngles.y;
                mouseY = target.eulerAngles.x;
            }
        }
        
        struct ClipPlanePoints
        {
            public Vector3 upperLeft;
            public Vector3 upperRight;
            public Vector3 lowerLeft;
            public Vector3 lowerRight;
        }
    }
}