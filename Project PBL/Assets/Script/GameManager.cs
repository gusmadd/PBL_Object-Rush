using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 60f; // 5 Menit

    [Header("Network Sync States")]
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(60f);
    public NetworkVariable<bool> IsGameActive = new NetworkVariable<bool>(false);

    public Dictionary<ulong, int> PlayerScores { get; private set; } = new Dictionary<ulong, int>();
    private Dictionary<ulong, float> lastScoreTime = new Dictionary<ulong, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            IsGameActive.Value = false; // KUNCI: Game masih MATI saat di Room Lobby
            TimeRemaining.Value = matchDuration;
        }

        // Pasang listener: Jika IsGameActive berubah jadi true, sembunyikan UI Menu di semua Client
        IsGameActive.OnValueChanged += OnGameActiveChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsGameActive.OnValueChanged -= OnGameActiveChanged;
    }

    // DIPANGGIL OLEH HOST SAAT TEKAN TOMBOL "START GAME" DI ROOM PANEL
    public void StartGameFromHost()
    {
        if (!IsServer) return;

        IsGameActive.Value = true; // RESMI MULAI GAME
        TimeRemaining.Value = matchDuration;

        // Spawn bola pertama di arena
        if (ObjectiveSpawner.Instance != null)
        {
            ObjectiveSpawner.Instance.SpawnRandomObjective();
        }
    }

    private void OnGameActiveChanged(bool previousValue, bool newValue)
    {
        // Jika game diaktifkan, sembunyikan semua Panel Menu di layar player
        if (newValue == true)
        {
            if (NetworkUI.Instance != null)
            {
                NetworkUI.Instance.HideAllPanels();
            }
        }
    }

    private void Update()
    {
        // Hanya jalan jika game SUDAH AKTIF
        if (!IsServer || !IsGameActive.Value) return;

        if (TimeRemaining.Value > 0)
        {
            TimeRemaining.Value -= Time.deltaTime;
        }
        else
        {
            TimeRemaining.Value = 0;
            EndMatch();
        }
    }

    public int GetScore(ulong clientId)
    {
        return PlayerScores.ContainsKey(clientId) ? PlayerScores[clientId] : 0;
    }

    public void AddScore(ulong clientId, int points)
    {
        if (!IsServer || !IsGameActive.Value) return;

        if (!PlayerScores.ContainsKey(clientId))
        {
            PlayerScores[clientId] = 0;
            lastScoreTime[clientId] = Time.time;
        }

        PlayerScores[clientId] += points;
        lastScoreTime[clientId] = Time.time;

        UpdateScoreClientRpc(clientId, PlayerScores[clientId]);
        Debug.Log($"<color=cyan>[SCORE] Player {clientId} mendapatkan {points} poin. Total Skor: {PlayerScores[clientId]}</color>");
    }

    [ClientRpc]
    private void UpdateScoreClientRpc(ulong clientId, int newScore)
    {
        PlayerScores[clientId] = newScore;
    }

    private void EndMatch()
    {
        IsGameActive.Value = false;
        Debug.Log("<color=yellow>[GAME OVER] Waktu Habis!</color>");

        ulong winnerId = 0;
        int highestScore = -1;
        float earliestGoalTime = float.MaxValue;

        foreach (var entry in PlayerScores)
        {
            ulong pId = entry.Key;
            int score = entry.Value;
            float timeAchieved = lastScoreTime.ContainsKey(pId) ? lastScoreTime[pId] : float.MaxValue;

            if (score > highestScore)
            {
                highestScore = score;
                earliestGoalTime = timeAchieved;
                winnerId = pId;
            }
            else if (score == highestScore && score > 0)
            {
                if (timeAchieved < earliestGoalTime)
                {
                    earliestGoalTime = timeAchieved;
                    winnerId = pId;
                }
            }
        }

        NotifyGameOverClientRpc(winnerId, highestScore);
    }

    [ClientRpc]
    private void NotifyGameOverClientRpc(ulong winnerClientId, int finalScore)
    {
        Debug.Log($"<color=green>[WINNER] Player {winnerClientId} Menang! dengan skor {finalScore}</color>");
    }
}