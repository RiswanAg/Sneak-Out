using UnityEngine;
using Photon.Pun;

public class SwitchLight : MonoBehaviourPun
{
    public Light[] lightsToControl; // Lampu yang kau nak kawal
    public KeyCode interactKey = KeyCode.E;

    private bool playerNear = false;
    private bool isOn = false;

    void Update()
    {
        if (playerNear && Input.GetKeyDown(interactKey))
        {
            // Toggle locally and sync across network
            isOn = !isOn;
            photonView.RPC("RPC_ToggleLights", RpcTarget.AllBuffered, isOn);
        }
    }

    [PunRPC]
    void RPC_ToggleLights(bool state)
    {
        isOn = state;
        foreach (Light l in lightsToControl)
        {
            if (l != null)
                l.enabled = state;
        }
        
        Debug.Log($"Lights toggled: {state}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Only respond to local player
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                playerNear = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                playerNear = false;
            }
        }
    }
}