using UnityEngine;

public class LockerTwoDoor : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;

    public float openAngle = 90f;
    public float openSpeed = 4f;

    private bool isOpen = false;
    private bool playerNear = false;

    private Quaternion leftClosedRot, leftOpenRot;
    private Quaternion rightClosedRot, rightOpenRot;

    void Start()
    {
        leftClosedRot = leftDoor.localRotation;
        rightClosedRot = rightDoor.localRotation;

        leftOpenRot = Quaternion.Euler(leftDoor.localEulerAngles + new Vector3(0, -openAngle, 0));
        rightOpenRot = Quaternion.Euler(rightDoor.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        if (playerNear && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
        }

        if (isOpen)
        {
            leftDoor.localRotation = Quaternion.Slerp(leftDoor.localRotation, leftOpenRot, Time.deltaTime * openSpeed);
            rightDoor.localRotation = Quaternion.Slerp(rightDoor.localRotation, rightOpenRot, Time.deltaTime * openSpeed);
        }
        else
        {
            leftDoor.localRotation = Quaternion.Slerp(leftDoor.localRotation, leftClosedRot, Time.deltaTime * openSpeed);
            rightDoor.localRotation = Quaternion.Slerp(rightDoor.localRotation, rightClosedRot, Time.deltaTime * openSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = false;
    }
}
