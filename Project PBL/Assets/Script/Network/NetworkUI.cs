using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Net.NetworkInformation;

public class NetworkUI : MonoBehaviour
{
    public static NetworkUI Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Button infoButton;

    [Header("Room Code UI (Baru)")]
    [SerializeField] private GameObject joinCodePanel;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinSubmitBtn;
    [SerializeField] private Button closeJoinPanelBtn; // [BARU] Tombol X / Back di panel input kode
    [SerializeField] private TextMeshProUGUI hostRoomCodeText;
    [SerializeField] private Button copyCodeBtn; // [BARU] Tombol Copy di sebelah teks kode
    [SerializeField] private TextMeshProUGUI errorCodeText;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI firstPlaceText;
    [SerializeField] private TextMeshProUGUI firstPlaceScoreText;
    [SerializeField] private TextMeshProUGUI secondPlaceText;
    [SerializeField] private TextMeshProUGUI secondPlaceScoreText;
    [SerializeField] private TextMeshProUGUI thirdPlaceText;
    [SerializeField] private TextMeshProUGUI thirdPlaceScoreText;
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

    [Header("Intro Panel UI")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject readyImage;
    [SerializeField] private GameObject goImage;
    [SerializeField] private GameObject thirdTimeImage;
    [SerializeField] private GameObject secondTimeImage;
    [SerializeField] private GameObject firstTimeImage;

    public static string LocalUsername = "Player";
    private readonly Dictionary<ulong, string> playerNameCache = new Dictionary<ulong, string>();

    // Variabel untuk menyimpan kode murni agar mudah di-copy
    private string currentRoomCode = "";
    private const int MaxUsernameLength = 15;
    private Animator infoPanelAnimator;
    private Button closeInfoPanelButton;
    private Coroutine infoPanelAnimationRoutine;

    private void Awake()
    {
        Instance = this;

        if (usernameInput != null)
        {
            usernameInput.characterLimit = MaxUsernameLength;
            if (usernameInput.text.Length > MaxUsernameLength)
            {
                usernameInput.text = usernameInput.text.Substring(0, MaxUsernameLength);
            }
        }

        mainMenuPanel.SetActive(true);
        roomPanel.SetActive(false);
        if (infoPanel != null) infoPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (joinCodePanel != null) joinCodePanel.SetActive(false);
        if (errorCodeText != null) errorCodeText.gameObject.SetActive(false);
        if (introPanel != null) introPanel.SetActive(false);

        if (infoPanel == null)
        {
            Debug.LogError("[NetworkUI] Info Panel belum di-assign.", this);
        }
        else
        {
            infoPanelAnimator = infoPanel.GetComponent<Animator>();
            if (infoPanelAnimator == null)
            {
                Debug.LogError("[NetworkUI] Animator tidak ditemukan pada Info Panel.", infoPanel);
            }

            Button[] infoPanelButtons = infoPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in infoPanelButtons)
            {
                if (!button.gameObject.activeSelf) continue;
                closeInfoPanelButton = button;
                break;
            }

            if (closeInfoPanelButton != null)
            {
                closeInfoPanelButton.onClick.AddListener(CloseInfoPanel);
            }
            else
            {
                Debug.LogError("[NetworkUI] Close Button tidak ditemukan di dalam Info Panel.", infoPanel);
            }
        }

        if (infoButton != null)
        {
            infoButton.onClick.AddListener(OpenInfoPanel);
        }
        else
        {
            Debug.LogError("[NetworkUI] Info Button belum di-assign.", this);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // --- TOMBOL HOST ---
        // --- TOMBOL HOST ---
        hostBtn.onClick.AddListener(() =>
        {
            SaveUsername();

            // PAKSA HOST MEMBUKA GERBANG UNTUK SEMUA JARINGAN (0.0.0.0)
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            }

            if (NetworkManager.Singleton.StartHost())
            {
                string localIP = GetLocalIPAddress();
                currentRoomCode = EncodeIPToRoomCode(localIP);

                if (hostRoomCodeText != null) hostRoomCodeText.text = $"Room code: {currentRoomCode}";

                OpenRoomPanel(isHost: true);
            }
        });

        // --- TOMBOL COPY KODE (BARU) ---
        if (copyCodeBtn != null)
        {
            copyCodeBtn.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(currentRoomCode))
                {
                    GUIUtility.systemCopyBuffer = currentRoomCode; // Salin ke Clipboard OS
                    Debug.Log($"<color=cyan>Room Code [{currentRoomCode}] disalin ke Clipboard!</color>");
                }
            });
        }

        // --- TOMBOL JOIN (MEMBUKA PANEL KODE) ---
        clientBtn.onClick.AddListener(() =>
        {
            SaveUsername();
           // mainMenuPanel.SetActive(false);
            if (joinCodePanel != null) joinCodePanel.SetActive(true);
        });

        // --- TOMBOL CLOSE/BACK PANEL JOIN (BARU) ---
        if (closeJoinPanelBtn != null)
        {
            closeJoinPanelBtn.onClick.AddListener(() =>
            {
                if (joinCodePanel != null) joinCodePanel.SetActive(false);
                mainMenuPanel.SetActive(true); // Tampilkan menu awal lagi
            });
        }

        // --- TOMBOL SUBMIT KODE DI PANEL JOIN ---
        // --- TOMBOL SUBMIT KODE DI PANEL JOIN ---
        if (joinSubmitBtn != null)
        {
            joinSubmitBtn.onClick.AddListener(() =>
            {
                string inputCode = joinCodeInput.text.Trim().ToUpper();
                string targetIP = DecodeRoomCodeToIP(inputCode);

                if (string.IsNullOrEmpty(targetIP))
                {
                    StartCoroutine(BlinkErrorTextRoutine());
                    return;
                }

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData(targetIP, 7777);
                }

                if (NetworkManager.Singleton.StartClient())
                {
                    if (joinCodePanel != null) joinCodePanel.SetActive(false);
                    if (hostRoomCodeText != null) hostRoomCodeText.text = $"Room code: {inputCode}";

                    currentRoomCode = inputCode;
                    OpenRoomPanel(isHost: false);

                    // PASANG CCTV: Tampilkan IP target di layar tunggu
                    if (waitingText != null)
                    {
                        waitingText.text = $"Mencoba masuk ke IP:\n{targetIP}\n\nTunggu host memulai...";
                    }
                }
            });
        }

        quitBtn.onClick.AddListener(() => Application.Quit());

        leaveRoomBtn.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            CloseRoomPanel();
        });

        if (backToMainMenuBtn != null) backToMainMenuBtn.onClick.AddListener(ReturnToMainMenu);

        startGameBtn.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.IsServer && GameManager.Instance != null)
            {
                GameManager.Instance.StartGameFromHost();
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
        if (infoButton != null) infoButton.onClick.RemoveListener(OpenInfoPanel);
        if (closeInfoPanelButton != null) closeInfoPanelButton.onClick.RemoveListener(CloseInfoPanel);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OpenInfoPanel()
    {
        if (infoPanel == null || infoPanelAnimator == null) return;

        if (infoPanelAnimationRoutine != null)
        {
            StopCoroutine(infoPanelAnimationRoutine);
            infoPanelAnimationRoutine = null;
        }

        if (closeInfoPanelButton != null) closeInfoPanelButton.interactable = true;
        if (infoButton != null) infoButton.gameObject.SetActive(false);
        infoPanel.SetActive(true);
        infoPanelAnimator.Play("Base Layer.In", 0, 0f);
    }

    private void CloseInfoPanel()
    {
        if (infoPanel == null || infoPanelAnimator == null || !infoPanel.activeSelf) return;
        if (infoPanelAnimationRoutine != null) StopCoroutine(infoPanelAnimationRoutine);

        if (closeInfoPanelButton != null) closeInfoPanelButton.interactable = false;
        infoPanelAnimationRoutine = StartCoroutine(PlayInfoPanelOutRoutine());
    }

    private System.Collections.IEnumerator PlayInfoPanelOutRoutine()
    {
        infoPanelAnimator.Play("Base Layer.Out", 0, 0f);
        yield return null;

        AnimatorStateInfo stateInfo = infoPanelAnimator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("Base Layer.Out"))
        {
            Debug.LogError("[NetworkUI] State animasi Out tidak dapat diputar.", infoPanel);
            infoPanel.SetActive(false);
            if (infoButton != null) infoButton.gameObject.SetActive(true);
            infoPanelAnimationRoutine = null;
            yield break;
        }

        while (stateInfo.normalizedTime < 1f)
        {
            yield return null;
            stateInfo = infoPanelAnimator.GetCurrentAnimatorStateInfo(0);
        }

        infoPanel.SetActive(false);
        if (infoButton != null) infoButton.gameObject.SetActive(true);
        infoPanelAnimationRoutine = null;
    }

    // ==========================================
    // IP TO ROOM CODE ENCODER & DECODER
    // ==========================================
    private string EncodeIPToRoomCode(string ip)
    {
        try
        {
            byte[] ipBytes = IPAddress.Parse(ip).GetAddressBytes();

            // 1. Buat Kunci Acak (1 - 254) setiap kali Host bikin Room
            byte randomKey = (byte)UnityEngine.Random.Range(1, 255);

            byte[] encodedBytes = new byte[6];

            // 2. Acak IP menggunakan Kunci Acak tersebut (XOR Encryption)
            for (int i = 0; i < 4; i++)
            {
                encodedBytes[i] = (byte)(ipBytes[i] ^ randomKey);
            }

            // 3. Sisipkan kunci acaknya di urutan paling belakang
            encodedBytes[4] = randomKey;

            encodedBytes[5] = CalculateRoomCodeChecksum(encodedBytes, 5);

            // Hasilnya 12 karakter: 10 karakter data dan 2 karakter checksum.
            return System.BitConverter.ToString(encodedBytes).Replace("-", "");
        }
        catch { return "ERROR"; }
    }

    private string DecodeRoomCodeToIP(string code)
    {
        try
        {
            // Jika kurang/lebih dari 12 karakter, langsung tolak
            if (code.Length != 12) return null;

            byte[] bytes = new byte[6];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(code.Substring(i * 2, 2), 16);
            }

            if (bytes[5] != CalculateRoomCodeChecksum(bytes, 5)) return null;

            byte key = bytes[4];
            byte[] decodedIpBytes = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                decodedIpBytes[i] = (byte)(bytes[i] ^ key);
            }

            return new IPAddress(decodedIpBytes).ToString();
        }
        catch { return null; } // Jika kodenya bukan angka/huruf yang valid, tolak
    }

    private static byte CalculateRoomCodeChecksum(byte[] bytes, int length)
    {
        byte checksum = 0;
        for (int i = 0; i < length; i++)
        {
            checksum ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
            {
                checksum = (byte)((checksum & 0x80) != 0
                    ? (checksum << 1) ^ 0x07
                    : checksum << 1);
            }
        }

        return checksum;
    }

    // ==========================================
    // SISA KODE UI (TETAP SAMA SEPERTI SEBELUMNYA)
    // ==========================================

    private void SaveUsername()
    {
        if (!string.IsNullOrEmpty(usernameInput.text))
        {
            LocalUsername = usernameInput.text.Length > MaxUsernameLength
                ? usernameInput.text.Substring(0, MaxUsernameLength)
                : usernameInput.text;
        }
        else LocalUsername = "Player " + Random.Range(1, 99);
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
        }

        UpdatePlayerSlots();
    }

    private void CloseRoomPanel()
    {
        roomPanel.SetActive(false);
        if (joinCodePanel != null) joinCodePanel.SetActive(false);
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
        if (string.IsNullOrWhiteSpace(name)) name = $"Player {clientId}";
        playerNameCache[clientId] = name;
        UpdatePlayerSlots();
    }

    public void RemovePlayerName(ulong clientId)
    {
        if (playerNameCache.ContainsKey(clientId)) playerNameCache.Remove(clientId);
        UpdatePlayerSlots();
    }

    private void OnClientConnected(ulong clientId) => UpdatePlayerSlots();

    private void OnClientDisconnected(ulong clientId)
    {
        RemovePlayerName(clientId);
        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public string GetDisplayNameForClient(ulong clientId)
    {
        if (playerNameCache.TryGetValue(clientId, out string cachedName) && !string.IsNullOrWhiteSpace(cachedName)) return cachedName;
        return $"Player {clientId}";
    }

    public void UpdatePlayerSlots()
    {
        if (NetworkManager.Singleton == null) return;

        // AMBIL DATA DARI CACHE LOKAL (Bisa dibaca oleh Host maupun Client)
        List<ulong> clientIds = new List<ulong>(playerNameCache.Keys);
        clientIds.Sort(); // Urutkan agar Host (ID 0) selalu di Slot 1, Client 1 di Slot 2, dst.

        int count = clientIds.Count;

        for (int i = 0; i < playerSlotTexts.Length; i++)
        {
            if (i < count)
            {
                ulong clientId = clientIds[i];
                string playerName = GetDisplayNameForClient(clientId);
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    playerName += " (You)";
                }

                playerSlotTexts[i].text = playerName;
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
        if (joinCodePanel != null) joinCodePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ShowGameOver(ulong[] rankedClientIds)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(out PlayerMovement playerMovement))
        {
            playerMovement.StopRunAudio();
        }

        if (gameOverPanel == null) return;

        SoundManager.Instance?.StopGameplayBgm();
        mainMenuPanel.SetActive(false);
        roomPanel.SetActive(false);
        gameOverPanel.SetActive(true);

        SetRankText(firstPlaceText, rankedClientIds, 0);
        SetRankScoreText(firstPlaceScoreText, rankedClientIds, 0);
        SetRankText(secondPlaceText, rankedClientIds, 1);
        SetRankScoreText(secondPlaceScoreText, rankedClientIds, 1);
        SetRankText(thirdPlaceText, rankedClientIds, 2);
        SetRankScoreText(thirdPlaceScoreText, rankedClientIds, 2);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetRankText(TextMeshProUGUI rankText, ulong[] rankedClientIds, int rankIndex)
    {
        if (rankText == null) return;
        rankText.text = rankIndex < rankedClientIds.Length ? GetDisplayNameForClient(rankedClientIds[rankIndex]) : "No player";
    }

    private void SetRankScoreText(TextMeshProUGUI scoreText, ulong[] rankedClientIds, int rankIndex)
    {
        if (scoreText == null) return;
        scoreText.text = rankIndex < rankedClientIds.Length
            ? $"{GameManager.Instance.GetScore(rankedClientIds[rankIndex])} PTS"
            : string.Empty;
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    private string GetLocalIPAddress()
    {
        string fallbackIP = "127.0.0.1";

        // Melakukan scan ke seluruh adapter jaringan di laptop
        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Abaikan adapter yang mati, loopback, atau aplikasi Virtual (VirtualBox, VMware, WSL, dll)
            if (item.OperationalStatus != OperationalStatus.Up ||
                item.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                item.Description.ToLower().Contains("virtual") ||
                item.Description.ToLower().Contains("vmware") ||
                item.Description.ToLower().Contains("wsl") ||
                item.Description.ToLower().Contains("hyper-v"))
            {
                continue;
            }

            foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    fallbackIP = ip.Address.ToString();

                    // Jika ini adalah adapter Wi-Fi (Wireless) atau Kabel LAN (Ethernet), langsung pilih ini!
                    if (item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                        item.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        Debug.Log($"<color=green>[NetworkUI] IP Valid Ditemukan: {fallbackIP} ({item.Description})</color>");
                        return fallbackIP;
                    }
                }
            }
        }

        Debug.LogWarning($"<color=yellow>[NetworkUI] Menggunakan IP Fallback: {fallbackIP}</color>");
        return fallbackIP;
    }
    private System.Collections.IEnumerator BlinkErrorTextRoutine()
    {
        if (errorCodeText == null) yield break;

        errorCodeText.gameObject.SetActive(true);
        errorCodeText.text = "Code room salah!";
        errorCodeText.color = Color.red; // Pastikan warnanya merah

        // Bikin kedap-kedip 3 kali
        for (int i = 0; i < 3; i++)
        {
            errorCodeText.alpha = 0f; // Teks menghilang (transparan)
            yield return new WaitForSeconds(0.15f);
            errorCodeText.alpha = 1f; // Teks muncul
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(1.5f); // Tahan sebentar di layar
        errorCodeText.gameObject.SetActive(false); // Sembunyikan lagi
    }
    // --- COROUTINE ANIMASI ABA-ABA (COUNTDOWN) ---
    public System.Collections.IEnumerator PlayIntroCountdownRoutine()
    {
        // 1. Nyalakan Panel Intro secara keseluruhan
        if (introPanel != null) introPanel.SetActive(true);

        // Matikan semua gambar aba-aba di awal
        SetAllIntroImages(false);

        // 5. Tampilkan teks "READY"
        if (readyImage != null) readyImage.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        if (readyImage != null) readyImage.SetActive(false);

        // 2. Tampilkan angka "3" (3 detik)
        SoundManager.Instance?.PlayIntroSfx();
        if (thirdTimeImage != null) thirdTimeImage.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (thirdTimeImage != null) thirdTimeImage.SetActive(false);

        // 3. Tampilkan angka "2" (2 detik)
        if (secondTimeImage != null) secondTimeImage.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (secondTimeImage != null) secondTimeImage.SetActive(false);

        // 4. Tampilkan angka "1" (1 detik)
        if (firstTimeImage != null) firstTimeImage.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (firstTimeImage != null) firstTimeImage.SetActive(false);

        // 6. Tampilkan teks "GO!"
        if (goImage != null) goImage.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (goImage != null) goImage.SetActive(false);

        // 7. Matikan Panel Intro sepenuhnya agar permainan dimulai
        if (introPanel != null) introPanel.SetActive(false);
    }

    private void SetAllIntroImages(bool state)
    {
        if (thirdTimeImage != null) thirdTimeImage.SetActive(state);
        if (secondTimeImage != null) secondTimeImage.SetActive(state);
        if (firstTimeImage != null) firstTimeImage.SetActive(state);
        if (readyImage != null) readyImage.SetActive(state);
        if (goImage != null) goImage.SetActive(state);
    }
}