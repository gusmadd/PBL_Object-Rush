using Unity.Netcode;
using UnityEngine;

public class HomeZone : NetworkBehaviour
{
    [Header("Home Zone Settings")]
    [Tooltip("ID Pemilik Rumah ini (0 untuk Player 1/Host, 1 untuk Player 2, dst)")]
    [SerializeField] private ulong zoneOwnerClientId = 0;

    [Header("Local Visual Effect")]
    [Tooltip("Komponen Light yang dipasang di dalam Home (Spotlight / Point Light)")]
    [SerializeField] private GameObject homeLight;

    public ulong ZoneOwnerClientId => zoneOwnerClientId;

    private void Awake()
    {
        // Pastikan lampu mati terlebih dahulu saat game baru start
        if (homeLight != null)
        {
            homeLight.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Cek apakah ID Client lokal di PC ini sama dengan ID pemilik rumah ini
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == zoneOwnerClientId)
        {
            if (homeLight != null)
            {
                homeLight.SetActive(true); // Nyalakan cahaya KHUSUS untuk pemilik rumah di PC ini
                Debug.Log($"<color=yellow>[HomeZone] Efek cahaya dinyalakan untuk Rumah Player {zoneOwnerClientId}!</color>");
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.TryGetComponent<ObjectiveItem>(out var objective))
        {
            if (!objective.IsCarried.Value)
            {
                if (collision.gameObject.TryGetComponent<NetworkObject>(out var netObj))
                {
                    if (netObj != null && netObj.IsSpawned)
                    {
                        Debug.Log($"<color=green>[GOAL!] Objective mengenai Rumah Player {ZoneOwnerClientId}!</color>");

                        // 1. Tambah +10 Skor ke Pemilik Rumah lewat GameManager
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.AddScore(ZoneOwnerClientId, 10);
                        }

                        // 2. Despawn & Respawn bola baru di area
                        if (ObjectiveSpawner.Instance != null)
                        {
                            ObjectiveSpawner.Instance.DespawnAndRespawnNext(netObj);
                        }
                    }
                }
            }
        }
    }
}