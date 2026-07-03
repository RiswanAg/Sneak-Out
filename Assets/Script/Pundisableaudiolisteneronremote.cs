using UnityEngine;
using Photon.Pun;

public class PunDisableAudioListenerOnRemote : MonoBehaviourPun
{
    void Awake()
    {
        // If this is NOT the local player, disable the Audio Listener
        if (!photonView.IsMine)
        {
            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
                Debug.Log($"[Audio] Disabled Audio Listener on remote player: {gameObject.name}");
            }
        }
    }
}