using System.Collections.Generic;
using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 60f; // 5 Menit

    [Header("Network Sync States")]
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(60f);
    public NetworkVariable<bool> IsGameActive = new NetworkVariable<bool>(false);

    [Header("Cinemachine Intro")]
    [SerializeField] private GameObject introCamera; // Masukkan IntroCamera dari scene ke sini
    [SerializeField] private Animator introAnimator; // Durasi animasi intro dalam detik
    private CinemachineInputAxisController playerCameraInputController;
    private CinemachineOrbitalFollow playerCameraOrbitalFollow;
    // Status apakah intro sedang berjalan (agar player tidak bisa gerak dulu)
    public bool IsIntroPlaying { get; private set; } = false;
    private bool hasIntroFinished;

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

        GameObject playerCamera = GameObject.Find("PlayerCamera");
        if (playerCamera != null)
        {
            playerCamera.TryGetComponent(out playerCameraInputController);
            playerCamera.TryGetComponent(out playerCameraOrbitalFollow);
        }

        if (introCamera != null)
        {
            introAnimator.enabled = false; // Pastikan IntroCamera mati saat awal
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            IsGameActive.Value = false; // KUNCI: Game masih MATI saat di Room Lobby
            TimeRemaining.Value = matchDuration;
            hasIntroFinished = false;
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

        hasIntroFinished = false;
        IsGameActive.Value = true; // RESMI MULAI GAME
        TimeRemaining.Value = matchDuration;
    }

    private void OnGameActiveChanged(bool previousValue, bool newValue)
    {
        if (newValue == true)
        {
            if (NetworkUI.Instance != null)
            {
                NetworkUI.Instance.HideAllPanels();
            }

            // Jalankan transisi kamera
            StartCoroutine(PlayIntroRoutine());
        }
    }

    private System.Collections.IEnumerator PlayIntroRoutine()
    {
        IsIntroPlaying = true;
        if (playerCameraInputController == null)
        {
            Debug.LogError("[GameManager] CinemachineInputAxisController pada PlayerCamera tidak ditemukan.");
        }
        else
        {
            playerCameraInputController.enabled = false;
        }

        if (playerCameraOrbitalFollow != null)
        {
            playerCameraOrbitalFollow.HorizontalAxis.Value = 0f;
            playerCameraOrbitalFollow.VerticalAxis.Value = playerCameraOrbitalFollow.VerticalAxis.Center;
        }
        else
        {
            Debug.LogError("[GameManager] CinemachineOrbitalFollow pada PlayerCamera tidak ditemukan.");
        }

        if (introCamera != null)
        {
            // 1. Nyalakan objek IntroCamera saat Start Game ditekan
            introAnimator.enabled = true;

            // 2. Pastikan komponen Animator-nya mereset ulang animasi ke awal
            Animator anim = introCamera.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play(0, -1, 0f); // Mulai animasi dari detik ke-0
            }
        }


        // Sesuaikan durasi ini dengan panjang animasi intromu (misal: 3 detik)
        yield return new WaitForSeconds(2.5f);

        // Setelah selesai, matikan IntroCamera agar beralih ke kamera player
        if (introCamera != null)
        {
            introCamera.SetActive(false);
        }

        yield return null;
        PlayerCameraFollow[] cameraFollows = FindObjectsByType<PlayerCameraFollow>(FindObjectsInactive.Exclude);
        foreach (PlayerCameraFollow cameraFollow in cameraFollows)
        {
            if (cameraFollow.IsOwner)
            {
                cameraFollow.ResetCameraBehindPlayer();
                break;
            }
        }

        yield return new WaitForSeconds(2f); // Tunggu sebentar sebelum countdown

        if (NetworkUI.Instance != null)
        {
            yield return NetworkUI.Instance.PlayIntroCountdownRoutine();
        }

        IsIntroPlaying = false; // Player sekarang bebas bergerak
        if (playerCameraInputController != null)
        {
            playerCameraInputController.enabled = true;
        }

        if (NetworkManager.Singleton.IsServer && ObjectiveSpawner.Instance != null)
        {
            ObjectiveSpawner.Instance.SpawnRandomObjective();
            Debug.Log("<color=green>[GameManager] Intro selesai, bola pertama resmi di-spawn!</color>");
        }

        if (IsServer)
        {
            hasIntroFinished = true;
        }
    }

    private void Update()
    {
        // Hanya jalan jika game SUDAH AKTIF
        if (!IsServer || !IsGameActive.Value || !hasIntroFinished) return;

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