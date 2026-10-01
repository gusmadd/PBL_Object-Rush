using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    public static NetworkUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI firstPlaceText;
    [SerializeField] private TextMeshProUGUI secondPlaceText;
    [SerializeField] private TextMeshProUGUI thirdPlaceText;
    [SerializeField] private Button backToMainMenuBtn;

    [Header("Main Menu UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private Button quitBtn;

    [Header("Room Panel UI - 4 Slot TMP Text")]
    [SerializeField] private TextMeshProUGUI[] playerSlotTexts;
    [SerializeField] private TextMeshProUGUI waitingText;
    [SerializeField] private Button startGameBtn;
    [SerializeField] private Button leaveRoomBtn;

    public static string LocalUsername = "Player";

    private readonly Dictionary<ulong, string> playerNameCache = new Dictionary<ulong, string>();

    private void Awake()
    {
        Instance = this;

        mainMenuPanel.SetActive(true);
        roomPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        hostBtn.onClick.AddListener(() =>
        {
            SaveUsername();
            if (NetworkManager.Singleton.StartHost())
            {
                OpenRoomPanel(isHost: true);
            }
        });

        clientBtn.onClick.AddListener(() =>
        {
            SaveUsername();
            if (NetworkManager.Singleton.StartClient())
            {
                OpenRoomPanel(isHost: false);
            }
        });

        quitBtn.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        leaveRoomBtn.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            CloseRoomPanel();
        });

        if (backToMainMenuBtn != null)
        {
            backToMainMenuBtn.onClick.AddListener(ReturnToMainMenu);
        }

        startGameBtn.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.IsServer)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.StartGameFromHost();
                }
            }
        });
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void SaveUsername()
    {
        if (!string.IsNullOrEmpty(usernameInput.text))
        {
            LocalUsername = usernameInput.text;
        }
        else
        {
            LocalUsername = "Player " + Random.Range(1, 99);
        }
    }

    private void OpenRoomPanel(bool isHost)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        roomPanel.SetActive(true);

        SetMainMenuButtonsInteractable(false);

        if (isHost)
        {
            startGameBtn.gameObject.SetActive(true);
            waitingText.gameObject.SetActive(false);
        }
        else
        {
            startGameBtn.gameObject.SetActive(false);
            waitingText.gameObject.SetActive(true);
            waitingText.text = "Tunggu host memulai game...";
        }

        UpdatePlayerSlots();
    }

    private void CloseRoomPanel()
    {
        roomPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        SetMainMenuButtonsInteractable(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetMainMenuButtonsInteractable(bool state)
    {
        if (hostBtn != null) hostBtn.interactable = state;
        if (clientBtn != null) clientBtn.interactable = state;
        if (quitBtn != null) quitBtn.interactable = state;
        if (usernameInput != null) usernameInput.interactable = state;
    }

    public void RegisterPlayerName(ulong clientId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Player {clientId}";
        }

        playerNameCache[clientId] = name;
        UpdatePlayerSlots();
    }

    public void RemovePlayerName(ulong clientId)
    {
        if (playerNameCache.ContainsKey(clientId))
        {
            playerNameCache.Remove(clientId);
        }

        UpdatePlayerSlots();
    }

    private void OnClientConnected(ulong clientId)
    {
        UpdatePlayerSlots();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RemovePlayerName(clientId);

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            CloseRoomPanel();
        }
    }

    public string GetDisplayNameForClient(ulong clientId)
    {
        if (playerNameCache.TryGetValue(clientId, out string cachedName) && !string.IsNullOrWhiteSpace(cachedName))
        {
            return cachedName;
        }

        return $"Player {clientId}";
    }

    public void UpdatePlayerSlots()
    {
        if (NetworkManager.Singleton == null) return;

        IReadOnlyList<ulong> connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        int count = connectedClients.Count;

        for (int i = 0; i < playerSlotTexts.Length; i++)
        {
            if (i < count)
            {
                ulong clientId = connectedClients[i];
                playerSlotTexts[i].text = GetDisplayNameForClient(clientId);
            }
            else
            {
                playerSlotTexts[i].text = $"Slot {i + 1}: Kosong...";
            }
        }

        // Tombol Start Game hanya aktif untuk Host jika player 2 s.d. 4
        if (NetworkManager.Singleton.IsHost)
        {
            startGameBtn.interactable = (count >= 2 && count <= 4);
        }
    }

    public void HideAllPanels()
    {
        mainMenuPanel.SetActive(false);
        roomPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ShowGameOver(ulong[] rankedClientIds)
    {
        if (gameOverPanel == null) return;

        mainMenuPanel.SetActive(false);
        roomPanel.SetActive(false);
        gameOverPanel.SetActive(true);

        SetRankText(firstPlaceText, rankedClientIds, 0);
        SetRankText(secondPlaceText, rankedClientIds, 1);
        SetRankText(thirdPlaceText, rankedClientIds, 2);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetRankText(TextMeshProUGUI rankText, ulong[] rankedClientIds, int rankIndex)
    {
        if (rankText == null) return;

        rankText.text = rankIndex < rankedClientIds.Length
            ? GetDisplayNameForClient(rankedClientIds[rankIndex])
            : "No player";
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        CloseRoomPanel();
    }
}