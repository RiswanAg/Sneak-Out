using UnityEngine;
using TMPro;
using Photon.Realtime;
using Photon.Pun.Demo.Cockpit;

public class RoomButtonScript : MonoBehaviour
{
    public TMP_Text buttonText;
    private RoomInfo info;

    public void SetButtonDetails(RoomInfo inputInfo)
    {
        info = inputInfo;

        buttonText.text = info.Name;
    }
    
    public void OpenRoom()
    {
        Launcher.instance.JoinRoom(info);
    }
}
