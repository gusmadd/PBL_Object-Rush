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
    private Dictionary<ulong, ulong> scoreOrder = new Dictionary<ulong, ulong>();
    private ulong nextScoreOrder;

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

        PlayerScores.Clear();
        scoreOrder.Clear();
        nextScoreOrder = 0;
        ResetScoresClientRpc();

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

    public ulong GetScoreOrder(ulong clientId)
    {
        return scoreOrder.TryGetValue(clientId, out ulong order) ? order : ulong.MaxValue;
    }

    public void AddScore(ulong clientId, int points)
    {
        if (!IsServer || !IsGameActive.Value) return;

        if (!PlayerScores.ContainsKey(clientId))
        {
            PlayerScores[clientId] = 0;
        }

        PlayerScores[clientId] += points;
        scoreOrder[clientId] = ++nextScoreOrder;

        UpdateScoreClientRpc(clientId, PlayerScores[clientId], scoreOrder[clientId]);
        Debug.Log($"<color=cyan>[SCORE] Player {clientId} mendapatkan {points} poin. Total Skor: {PlayerScores[clientId]}</color>");
    }

    [ClientRpc]
    private void UpdateScoreClientRpc(ulong clientId, int newScore, ulong newScoreOrder)
    {
        PlayerScores[clientId] = newScore;
        scoreOrder[clientId] = newScoreOrder;
    }

    private void EndMatch()
    {
        IsGameActive.Value = false;
        Debug.Log("<color=yellow>[GAME OVER] Waktu Habis!</color>");

        if (NetworkManager.Singleton == null) return;

        List<ulong> rankedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        Dictionary<ulong, int> connectionOrder = new Dictionary<ulong, int>();
        for (int i = 0; i < rankedClients.Count; i++)
        {
            connectionOrder[rankedClients[i]] = i;
        }

        rankedClients.Sort((firstId, secondId) =>
        {
            int scoreComparison = GetScore(secondId).CompareTo(GetScore(firstId));
            if (scoreComparison != 0) return scoreComparison;

            int scoreOrderComparison = GetScoreOrder(firstId).CompareTo(GetScoreOrder(secondId));
            if (scoreOrderComparison != 0) return scoreOrderComparison;

            return connectionOrder[firstId].CompareTo(connectionOrder[secondId]);
        });

        NotifyGameOverClientRpc(rankedClients.ToArray());
    }

    [ClientRpc]
    private void ResetScoresClientRpc()
    {
        PlayerScores.Clear();
        scoreOrder.Clear();
        nextScoreOrder = 0;
    }

    [ClientRpc]
    private void NotifyGameOverClientRpc(ulong[] rankedClientIds)
    {
        Time.timeScale = 0f;

        if (NetworkUI.Instance != null)
        {
            NetworkUI.Instance.ShowGameOver(rankedClientIds);
        }

        if (rankedClientIds.Length > 0)
        {
            Debug.Log($"<color=green>[WINNER] Player {rankedClientIds[0]} Menang dengan skor {GetScore(rankedClientIds[0])}</color>");
        }
    }
}