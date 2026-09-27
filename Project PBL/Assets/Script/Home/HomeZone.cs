using Unity.Netcode;
using UnityEngine;

public class HomeZone : NetworkBehaviour
{
    [Header("Home Zone Settings")]
    [Tooltip("ID Pemilik Rumah ini (0 untuk Player 1/Host, 1 untuk Player 2, dst)")]
    [SerializeField] private ulong zoneOwnerClientId = 0;

    [Header("Local Visual Effect")]
    [Tooltip("Komponen Light yang dipasang di dalam Home (Spotlight / Point Light)")]
    [SerializeField] private Light homeLight;

    public ulong ZoneOwnerClientId => zoneOwnerClientId;

    private void Awake()
    {
        // Pastikan lampu mati terlebih dahulu saat game baru start
        if (homeLight != null)
        {
            homeLight.enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Cek apakah ID Client lokal di PC ini sama dengan ID pemilik rumah ini
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == zoneOwnerClientId)
        {
            if (homeLight != null)
            {
                homeLight.enabled = true; // Nyalakan cahaya KHUSUS untuk pemilik rumah di PC ini
                Debug.Log($"<color=yellow>[HomeZone] Efek cahaya dinyalakan untuk Rumah Player {zoneOwnerClientId}!</color>");
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Cek apakah objek yang menabrak adalah ObjectiveItem
        if (collision.gameObject.TryGetComponent<ObjectiveItem>(out var objective))
        {
            if (!objective.IsCarried.Value)
            {
                if (collision.gameObject.TryGetComponent<NetworkObject>(out var netObj))
                {
                    if (netObj != null && netObj.IsSpawned)
                    {
                        Debug.Log($"<color=green>[GOAL!] Objective mengenai Rumah Player {zoneOwnerClientId}!</color>");

                        if (ObjectiveSpawner.Instance != null)
                        {
                            ObjectiveSpawner.Instance.DespawnAndRespawnNext(netObj);
                        }

                        // GameManager.Instance.AddScore(zoneOwnerClientId, 10);
                    }
                }
            }
        }
    }
}