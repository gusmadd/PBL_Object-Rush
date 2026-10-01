using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class InGameHUDManager : MonoBehaviour
{
    [Header("UI Canvas Panel")]
    [SerializeField] private GameObject hudPanel;

    [Header("Timer Display")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Scoreboard Displays (4 Player Slots)")]
    [SerializeField] private TextMeshProUGUI[] playerScoreTexts;

    private void Awake()
    {
        if (hudPanel != null)
        {
            hudPanel.SetActive(false); // Sembunyikan HUD saat masih di Menu Lobby
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // Tampilkan HUD hanya saat game resmi berjalan
        bool isGameActive = GameManager.Instance.IsGameActive.Value;
        if (hudPanel != null && hudPanel.activeSelf != isGameActive)
        {
            hudPanel.SetActive(isGameActive);
        }

        if (!isGameActive) return;

        // 1. Update Timer Format MM:SS
        UpdateTimerDisplay(GameManager.Instance.TimeRemaining.Value);

        // 2. Update Live Scoreboard
        UpdateScoreboardDisplay();
    }

    private void UpdateTimerDisplay(float timeInSeconds)
    {
        if (timerText == null) return;

        if (timeInSeconds < 0) timeInSeconds = 0;

        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        timerText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);
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

                playerScoreTexts[i].text = $"{playerName}: {score} PTS";
            }
            else
            {
                playerScoreTexts[i].text = ""; // Kosongkan slot jika player < 4
            }
        }
    }
}