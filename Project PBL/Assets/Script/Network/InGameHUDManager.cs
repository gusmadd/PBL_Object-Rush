using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class InGameHUDManager : MonoBehaviour
{
    public static InGameHUDManager Instance { get; private set; }

    [Header("UI Canvas Panel")]
    [SerializeField] private GameObject hudPanel;


    [Header("Sledding Cooldown UI")]
    public GameObject sleddingCooldownBackground;
    public Image sleddingCooldownFill;

    [Header("Timer Display")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Ping Display")]
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField, Min(0.1f)] private float pingRefreshInterval = 0.5f;

    [Header("Scoreboard Displays (4 Player Slots)")]
    [SerializeField] private TextMeshProUGUI[] playerScoreTexts;

    private float nextPingUpdateTime;

    private void Awake()
    {
        Instance = this;

        if (hudPanel != null)
        {
            hudPanel.SetActive(false); // Sembunyikan HUD saat masih di Menu Lobby
        }

        CacheSleddingCooldownUI();
        HideSleddingCooldown();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // Tampilkan HUD setelah intro dan countdown selesai.
        bool shouldShowHud = GameManager.Instance.IsGameActive.Value && !GameManager.Instance.IsIntroPlaying;
        if (hudPanel != null && hudPanel.activeSelf != shouldShowHud)
        {
            hudPanel.SetActive(shouldShowHud);
        }

        if (!shouldShowHud)
        {
            if (pingText != null)
            {
                pingText.text = "Ping: -- ms";
            }

            return;
        }

        // 1. Update Timer Format MM:SS
        UpdateTimerDisplay(GameManager.Instance.TimeRemaining.Value);

        // 2. Update network latency
        UpdatePingDisplay();

        // 3. Update Live Scoreboard
        UpdateScoreboardDisplay();
    }

    private void UpdatePingDisplay()
    {
        if (pingText == null || Time.unscaledTime < nextPingUpdateTime)
            return;

        nextPingUpdateTime = Time.unscaledTime + pingRefreshInterval;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null
            || !networkManager.IsListening
            || networkManager.NetworkConfig.NetworkTransport == null)
        {
            pingText.text = "Ping: -- ms";
            return;
        }

        ulong roundTripTime = networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(
            NetworkManager.ServerClientId
        );
        pingText.text = $"Ping: {roundTripTime} ms";
    }

    private void UpdateTimerDisplay(float timeInSeconds)
    {
        if (timerText == null) return;

        if (timeInSeconds < 0) timeInSeconds = 0;

        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void SetSleddingCooldown(float progress)
    {
        CacheSleddingCooldownUI();
        if (sleddingCooldownBackground != null)
        {
            sleddingCooldownBackground.SetActive(true);
        }

        if (sleddingCooldownFill != null)
        {
            sleddingCooldownFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    public void HideSleddingCooldown()
    {
        CacheSleddingCooldownUI();
        if (sleddingCooldownBackground != null)
        {
            sleddingCooldownBackground.SetActive(false);
        }
    }

    private void CacheSleddingCooldownUI()
    {
        if (sleddingCooldownBackground != null || hudPanel == null) return;

        Transform cooldownTransform = hudPanel.transform.Find("Sledding CD");
        if (cooldownTransform == null) return;

        sleddingCooldownBackground = cooldownTransform.gameObject;
        Transform fillTransform = cooldownTransform.Find("image/Fill");
        if (fillTransform != null)
        {
            sleddingCooldownFill = fillTransform.GetComponent<Image>();
        }
    }

    private void UpdateScoreboardDisplay()
    {
        if (NetworkManager.Singleton == null || playerScoreTexts == null) return;

        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        List<ulong> rankedClients = new List<ulong>(connectedClients);
        Dictionary<ulong, int> connectionOrder = new Dictionary<ulong, int>();

        for (int i = 0; i < connectedClients.Count; i++)
        {
            connectionOrder[connectedClients[i]] = i;
        }

        rankedClients.Sort((firstId, secondId) =>
        {
            int scoreComparison = GameManager.Instance.GetScore(secondId).CompareTo(GameManager.Instance.GetScore(firstId));
            if (scoreComparison != 0) return scoreComparison;

            int scoreOrderComparison = GameManager.Instance.GetScoreOrder(firstId).CompareTo(GameManager.Instance.GetScoreOrder(secondId));
            if (scoreOrderComparison != 0) return scoreOrderComparison;

            return connectionOrder[firstId].CompareTo(connectionOrder[secondId]);
        });

        for (int i = 0; i < playerScoreTexts.Length; i++)
        {
            if (i < rankedClients.Count)
            {
                ulong clientId = rankedClients[i];
                string playerName = $"Player {clientId}";
                int score = GameManager.Instance.GetScore(clientId);

                // Ambil nama dari cache yang sudah diupdate oleh PlayerNameTag.
                // Ini aman untuk semua client, karena tidak memanggil API server-only.
                if (NetworkUI.Instance != null)
                {
                    playerName = NetworkUI.Instance.GetDisplayNameForClient(clientId);
                }

                playerScoreTexts[i].text = $"{playerName} : {score} ";
                playerScoreTexts[i].color = clientId == NetworkManager.Singleton.LocalClientId
                    ? new Color32(108, 253, 77, 255)
                    : Color.white;
            }
            else
            {
                playerScoreTexts[i].text = ""; // Kosongkan slot jika player < 4
                playerScoreTexts[i].color = Color.white;
            }
        }
    }
}