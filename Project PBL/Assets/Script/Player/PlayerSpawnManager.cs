using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Daftar Lokasi Spawn Player")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    // Menyimpan Index Spawn Point yang sedang dipakai oleh Client tertentu
    private readonly Dictionary<ulong, int> clientSpawnIndices = new Dictionary<ulong, int>();
    // Daftar Index Spawn Point yang masih nganggur/kosong
    private List<int> availableSpawnIndices = new List<int>();

    private readonly Dictionary<ulong, Vector3> initialSpawnPositions = new Dictionary<ulong, Vector3>();
    private readonly HashSet<ulong> respawningClients = new HashSet<ulong>();

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

    private void Start()
    {
        // Masukkan semua index titik spawn ke daftar "Tersedia" (0, 1, 2, 3...)
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            availableSpawnIndices.Add(i);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            // Tambahkan listener untuk menangkap event saat player keluar
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

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[PlayerSpawnManager] Tidak ada spawn point yang terdaftar.");
            return;
        }

        Vector3 spawnPosition = AssignSpawnPosition(clientId);
        initialSpawnPositions[clientId] = spawnPosition;
        StartCoroutine(PlacePlayerWhenReady(clientId, spawnPosition));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Jika player yang keluar punya kursi/titik spawn, kembalikan kursinya menjadi "Tersedia"
        if (clientSpawnIndices.TryGetValue(clientId, out int freedIndex))
        {
            availableSpawnIndices.Add(freedIndex);
            availableSpawnIndices.Sort(); // Urutkan agar indeks terkecil diprioritaskan lagi

            clientSpawnIndices.Remove(clientId);
            initialSpawnPositions.Remove(clientId);

            Debug.Log($"[PlayerSpawnManager] Player {clientId} keluar. Spawn Point {freedIndex} kembali kosong.");
        }
    }

    private Vector3 AssignSpawnPosition(ulong clientId)
    {
        int assignedIndex = 0;

        // Ambil dari kursi yang kosong jika ada
        if (availableSpawnIndices.Count > 0)
        {
            assignedIndex = availableSpawnIndices[0];
            availableSpawnIndices.RemoveAt(0); // Hapus dari daftar kosong
        }
        else
        {
            // Jika anehnya kepenuhan, fallback ke index acak/modulus
            assignedIndex = (int)(clientId % (ulong)spawnPoints.Count);
        }

        // Catat kursi ini dipakai oleh siapa
        clientSpawnIndices[clientId] = assignedIndex;

        HomeZone[] allHomes = FindObjectsByType<HomeZone>(FindObjectsInactive.Exclude);
        foreach (HomeZone home in allHomes)
        {
            if (home.homeIndex == assignedIndex) // Jika indeks rumah cocok dengan indeks kursi
            {
                home.ZoneOwnerClientId.Value = clientId;
            }
        }

        // 4. Baris return HARUS ditaruh di paling bawah (Menyelesaikan Unreachable Code)
        return spawnPoints[assignedIndex].position;
    }

    public void RespawnPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (!initialSpawnPositions.TryGetValue(clientId, out Vector3 spawnPosition) || !respawningClients.Add(clientId))
            return;

        StartCoroutine(RespawnPlayerWhenReady(clientId, spawnPosition));
    }

    private IEnumerator RespawnPlayerWhenReady(ulong clientId, Vector3 spawnPosition)
    {
        yield return PlacePlayerWhenReady(clientId, spawnPosition);
        respawningClients.Remove(clientId);
    }

    private IEnumerator PlacePlayerWhenReady(ulong clientId, Vector3 spawnPosition)
    {
        NetworkObject playerNetObj = null;

        for (int attempt = 0; attempt < 30 && playerNetObj == null; attempt++)
        {
            playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetObj == null)
            {
                yield return null;
            }
        }

        if (playerNetObj == null)
        {
            Debug.LogError($"[PlayerSpawnManager] Player object untuk client {clientId} tidak ditemukan.");
            yield break;
        }

        Quaternion spawnRotation = Quaternion.identity;

        if (playerNetObj.TryGetComponent<NetworkTransform>(out var netTransform) && !netTransform.CanCommitToTransform)
        {
            PlayerMovement playerMovement = playerNetObj.GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                Debug.LogError($"[PlayerSpawnManager] PlayerMovement untuk client {clientId} tidak ditemukan.");
                yield break;
            }

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            playerMovement.TeleportOwnerClientRpc(spawnPosition, spawnRotation, clientRpcParams);
        }
        else
        {
            ApplyTeleport(playerNetObj, spawnPosition, spawnRotation);
        }
    }

    private void ApplyTeleport(NetworkObject playerNetObj, Vector3 position, Quaternion rotation)
    {
        if (playerNetObj.TryGetComponent<NetworkTransform>(out var netTransform))
        {
            netTransform.Teleport(position, rotation, playerNetObj.transform.localScale);
        }
        else
        {
            playerNetObj.transform.SetPositionAndRotation(position, rotation);
        }

        if (playerNetObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}