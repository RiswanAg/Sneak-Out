using UnityEngine;

public class SimpleThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 4f;
    public float height = 1.7f;
    public float rotationSpeed = 150f;

    float mouseX, mouseY;

    void LateUpdate()
    {
        if (target == null) return;

        // mouse input
        mouseX += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
        mouseY -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
        mouseY = Mathf.Clamp(mouseY, -35f, 60f);   // limit vertical angle

        // rotation around player
        Quaternion rotation = Quaternion.Euler(mouseY, mouseX, 0);
        
        // position camera behind player
        Vector3 offset = rotation * new Vector3(0, height, -distance);
        transform.position = target.position + offset;

        // look at player
        transform.LookAt(target.position + Vector3.up * height);
    }
}
