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

    private int currentSpawnIndex = 0;
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
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Hanya eksekusi di Server/Host
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[PlayerSpawnManager] Tidak ada spawn point yang terdaftar.");
            return;
        }

        Vector3 spawnPosition = GetNextSpawnPosition();
        initialSpawnPositions[clientId] = spawnPosition;
        StartCoroutine(PlacePlayerWhenReady(clientId, spawnPosition));
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

        Debug.Log($"[PlayerSpawnManager] Posisi spawn {spawnPosition} diproses untuk client {clientId}.");
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

    public Vector3 GetNextSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            return Vector3.zero;
        }

        Transform targetSpawn = spawnPoints[currentSpawnIndex % spawnPoints.Count];
        currentSpawnIndex++;

        return targetSpawn.position;
    }
}