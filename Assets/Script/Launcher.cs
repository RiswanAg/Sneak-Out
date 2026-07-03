using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

// Photon custom properties hashtable (fix "ambiguous Hashtable" error)
using Hashtable = ExitGames.Client.Photon.Hashtable;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Launcher : MonoBehaviourPunCallbacks
{
    public static Launcher instance;

    private void Awake()
    {
        instance = this;
    }

    public GameObject loadingScreen;
    public TMP_Text loadingText;

    public GameObject menuButtons;
    public GameObject createRoomScreen;
    public TMP_InputField roomNameInput;
    public GameObject roomScreen;
    public TMP_Text roomNameText, playerNameLabel;
    private List<TMP_Text> allPlayerNames = new List<TMP_Text>();

    public GameObject errorScreen;
    public TMP_Text errorText;

    public GameObject roomBrowserScreen;
    public GameObject contentObject;
    public RoomButtonScript theRoomButton;
    private List<RoomButtonScript> allRoomButtons = new List<RoomButtonScript>();

    public GameObject nameInputScreen;
    public TMP_InputField nameInput;
    private bool hasSetNick;

    public string levelToPlay = SceneNames.IntroCutscene;
    public GameObject startGameButton;

    public GameObject roomTestButton;

    void Start()
    {
        CloseMenus();

        if (loadingScreen) loadingScreen.SetActive(true);
        if (loadingText) loadingText.text = "Connecting to Network.";

        PhotonNetwork.ConnectUsingSettings();

#if UNITY_EDITOR
        if (roomTestButton) roomTestButton.SetActive(true);
#endif
    }

    void CloseMenus()
    {
        if (loadingScreen) loadingScreen.SetActive(false);
        if (menuButtons) menuButtons.SetActive(false);
        if (createRoomScreen) createRoomScreen.SetActive(false);
        if (roomScreen) roomScreen.SetActive(false);
        if (errorScreen) errorScreen.SetActive(false);
        if (roomBrowserScreen) roomBrowserScreen.SetActive(false);
        if (nameInputScreen) nameInputScreen.SetActive(false);
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
        PhotonNetwork.AutomaticallySyncScene = true;

        if (loadingText) loadingText.text = "Joining Lobby.";
    }

    public override void OnJoinedLobby()
    {
        CloseMenus();
        if (menuButtons) menuButtons.SetActive(true);

        // temp nickname until user sets one
        PhotonNetwork.NickName = Random.Range(0, 1000).ToString();

        if (!hasSetNick)
        {
            CloseMenus();
            if (nameInputScreen) nameInputScreen.SetActive(true);

            if (PlayerPrefs.HasKey("playerName") && nameInput)
            {
                nameInput.text = PlayerPrefs.GetString("playerName");
            }
        }
        else
        {
            if (PlayerPrefs.HasKey("playerName"))
                PhotonNetwork.NickName = PlayerPrefs.GetString("playerName");
        }
    }

    public void OpenRoomCreate()
    {
        CloseMenus();
        if (createRoomScreen) createRoomScreen.SetActive(true);
    }

    public void CreateRoom()
    {
        if (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
        {
            RoomOptions options = new RoomOptions();
            options.MaxPlayers = 2;

            PhotonNetwork.CreateRoom(roomNameInput.text, options);

            CloseMenus();
            if (loadingText) loadingText.text = "Creating Room.";
            if (loadingScreen) loadingScreen.SetActive(true);
        }
    }

    public override void OnJoinedRoom()
    {
        CloseMenus();
        if (roomScreen) roomScreen.SetActive(true);

        if (roomNameText) roomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // ✅ Assign character here (Hazim = Master, Amir = joiner)
        string character = PhotonNetwork.IsMasterClient ? "Hazim" : "Amir";
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "character", character } });

        ListAllPlayers();

        if (startGameButton)
            startGameButton.SetActive(PhotonNetwork.IsMasterClient);
    }

    private void ListAllPlayers()
    {
        foreach (TMP_Text player in allPlayerNames)
        {
            if (player) Destroy(player.gameObject);
        }
        allPlayerNames.Clear();

        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            TMP_Text newPlayerLabel = Instantiate(playerNameLabel, playerNameLabel.transform.parent);
            newPlayerLabel.text = players[i].NickName;
            newPlayerLabel.gameObject.SetActive(true);

            allPlayerNames.Add(newPlayerLabel);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        TMP_Text newPlayerLabel = Instantiate(playerNameLabel, playerNameLabel.transform.parent);
        newPlayerLabel.text = newPlayer.NickName;
        newPlayerLabel.gameObject.SetActive(true);

        allPlayerNames.Add(newPlayerLabel);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        ListAllPlayers();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (errorText) errorText.text = "Failed To Create Room: " + message;

        CloseMenus();
        if (errorScreen) errorScreen.SetActive(true);
    }

    public void CloseErrorScreen()
    {
        CloseMenus();
        if (menuButtons) menuButtons.SetActive(true);
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();

        CloseMenus();
        if (loadingText) loadingText.text = "Leaving Room.";
        if (loadingScreen) loadingScreen.SetActive(true);
    }

    public override void OnLeftRoom()
    {
        CloseMenus();
        if (menuButtons) menuButtons.SetActive(true);
    }

    public void openroomBrowser()
    {
        CloseMenus();
        if (roomBrowserScreen) roomBrowserScreen.SetActive(true);
    }

    public void CloseRoomBrowser()
    {
        CloseMenus();
        if (menuButtons) menuButtons.SetActive(true);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomButtonScript rb in allRoomButtons)
        {
            if (rb) Destroy(rb.gameObject);
        }
        allRoomButtons.Clear();

        if (theRoomButton) theRoomButton.gameObject.SetActive(false);

        for (int i = 0; i < roomList.Count; i++)
        {
            if (roomList[i].PlayerCount != roomList[i].MaxPlayers && !roomList[i].RemovedFromList)
            {
                RoomButtonScript newButton = Instantiate(theRoomButton, contentObject.transform);
                newButton.SetButtonDetails(roomList[i]);
                newButton.gameObject.SetActive(true);

                allRoomButtons.Add(newButton);
            }
        }
    }

    public void JoinRoom(RoomInfo info)
    {
        PhotonNetwork.JoinRoom(info.Name);

        CloseMenus();
        if (loadingText) loadingText.text = "Joining Room.";
        if (loadingScreen) loadingScreen.SetActive(true);
    }

    public void SetNickName()
    {
        if (nameInput != null && !string.IsNullOrEmpty(nameInput.text))
        {
            PhotonNetwork.NickName = nameInput.text;

            PlayerPrefs.SetString("playerName", nameInput.text);
            CloseMenus();
            if (menuButtons) menuButtons.SetActive(true);
            hasSetNick = true;
        }
    }

    public void StartGame()
    {
        PhotonNetwork.LoadLevel(levelToPlay);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (startGameButton)
            startGameButton.SetActive(PhotonNetwork.IsMasterClient);
    }

    public void QuickJoin()
    {
        RoomOptions options = new RoomOptions();
        options.MaxPlayers = 2;

        PhotonNetwork.CreateRoom("Test", options);

        CloseMenus();
        if (loadingText) loadingText.text = "Creating Room.";
        if (loadingScreen) loadingScreen.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}