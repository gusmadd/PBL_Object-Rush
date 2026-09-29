using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 180f; // 3 Menit
    [SerializeField] private int targetScore = 50;       // Skor maksimal untuk menang cepat

    [Header("Network Sync States")]
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(180f);
    public NetworkVariable<bool> IsGameActive = new NetworkVariable<bool>(false);

    // Dictionary lokal untuk menyimpan skor tiap ClientId
    public Dictionary<ulong, int> PlayerScores = new Dictionary<ulong, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            TimeRemaining.Value = matchDuration;
            IsGameActive.Value = true;
        }
    }

    private void Update()
    {
        if (!IsServer || !IsGameActive.Value) return;

        // Jalankan Countdown Timer di Server
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

    // Dipanggil dari HomeZone.cs ketika Player berhasil membawa bola ke goal
    public void AddScore(ulong clientId, int points)
    {
        if (!IsServer || !IsGameActive.Value) return;

        if (!PlayerScores.ContainsKey(clientId))
        {
            PlayerScores.Add(clientId, 0);
        }

        PlayerScores[clientId] += points;
        Debug.Log($"<color=green>[SCORE] Player ID {clientId} menambah {points} Poin! Total: {PlayerScores[clientId]}</color>");

        // Sync ke semua Client
        UpdateScoreClientRpc(clientId, PlayerScores[clientId]);

        // Cek jika sudah mencapai Target Skor
        if (PlayerScores[clientId] >= targetScore)
        {
            EndMatch();
        }
    }

    [ClientRpc]
    private void UpdateScoreClientRpc(ulong clientId, int newScore)
    {
        if (!PlayerScores.ContainsKey(clientId))
        {
            PlayerScores.Add(clientId, 0);
        }
        PlayerScores[clientId] = newScore;
    }

    private void EndMatch()
    {
        IsGameActive.Value = false;
        Debug.Log("<color=yellow>[GAME OVER] Pertandingan Selesai!</color>");

        // Cari pemenang dengan skor tertinggi
        ulong winnerId = 0;
        int highestScore = -1;

        foreach (var entry in PlayerScores)
        {
            if (entry.Value > highestScore)
            {
                highestScore = entry.Value;
                winnerId = entry.Key;
            }
        }

        NotifyGameOverClientRpc(winnerId, highestScore);
    }

    [ClientRpc]
    private void NotifyGameOverClientRpc(ulong winnerClientId, int finalScore)
    {
        Debug.Log($"[RESULT] Pemenangnya adalah Player ID: {winnerClientId} dengan Skor: {finalScore}");
        // Nanti di sini dipanggil UI Game Over / Victory Screen
    }
}