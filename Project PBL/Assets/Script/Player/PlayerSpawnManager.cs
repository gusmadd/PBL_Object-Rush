using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components; // Wajib import ini
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Daftar Lokasi Spawn Player")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    private int currentSpawnIndex = 0;

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

        StartCoroutine(PlacePlayerWhenReady(clientId));
    }

    private IEnumerator PlacePlayerWhenReady(ulong clientId)
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

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[PlayerSpawnManager] Tidak ada spawn point yang terdaftar.");
            yield break;
        }

        Vector3 spawnPosition = GetNextSpawnPosition();
        Quaternion spawnRotation = Quaternion.identity;

        playerNetObj.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (playerNetObj.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            rigidbody.position = spawnPosition;
            rigidbody.rotation = spawnRotation;

            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        Debug.Log($"[PlayerSpawnManager] Client {clientId} spawn di {spawnPosition}.");
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