using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class HomeZone : NetworkBehaviour
{
    [Header("Home Zone Settings")]
    [Tooltip("Indeks Kursi Rumah Ini (Misal: 0 untuk Rumah Pertama, 1 untuk Kedua)")]
    public int homeIndex = 0;

    // Pakai NetworkVariable agar kepemilikan rumah bisa di-update jika ada player yang reconnect (ID berubah)
    public NetworkVariable<ulong> ZoneOwnerClientId = new NetworkVariable<ulong>(999);
    public NetworkVariable<bool> IsHomeActive = new NetworkVariable<bool>(false);

    [Header("Local Visual Effect")]
    [Tooltip("Komponen Light yang dipasang di dalam Home (Spotlight / Point Light)")]
    [SerializeField] private GameObject homeLight;

    private bool isProcessingGoal = false; // Kunci anti-dobel skor
    private Renderer[] homeRenderers;
    private Collider[] homeColliders;

    private void Awake()
    {
        homeRenderers = GetComponentsInChildren<Renderer>(true);
        homeColliders = GetComponentsInChildren<Collider>(true);
        ApplyHomeActive(false);

        if (homeLight != null)
        {
            homeLight.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Pasang pendengar: Jika server mengubah pemilik rumah ini, jalankan fungsi UpdateLight
        ZoneOwnerClientId.OnValueChanged += OnOwnerChanged;
        IsHomeActive.OnValueChanged += OnHomeActiveChanged;

        // Setel lampu saat pertama kali spawn
        ApplyHomeActive(IsHomeActive.Value);
        UpdateLightEffect(ZoneOwnerClientId.Value);
    }

    public override void OnNetworkDespawn()
    {
        ZoneOwnerClientId.OnValueChanged -= OnOwnerChanged;
        IsHomeActive.OnValueChanged -= OnHomeActiveChanged;
    }

    public void SetAvailability(bool isActive, ulong ownerClientId)
    {
        if (!IsServer) return;

        ulong assignedOwner = isActive ? ownerClientId : 999;
        if (IsHomeActive.Value != isActive)
        {
            IsHomeActive.Value = isActive;
        }

        if (ZoneOwnerClientId.Value != assignedOwner)
        {
            ZoneOwnerClientId.Value = assignedOwner;
        }
    }

    private void OnHomeActiveChanged(bool previousValue, bool newValue)
    {
        ApplyHomeActive(newValue);
    }

    private void ApplyHomeActive(bool isActive)
    {
        foreach (Renderer homeRenderer in homeRenderers)
        {
            homeRenderer.enabled = isActive;
        }

        foreach (Collider homeCollider in homeColliders)
        {
            homeCollider.enabled = isActive;
        }
    }

    private void OnOwnerChanged(ulong previousOwner, ulong newOwner)
    {
        UpdateLightEffect(newOwner);
    }

    private void UpdateLightEffect(ulong currentOwnerId)
    {
        // Nyalakan lampu HANYA jika ID Client di layar PC ini sama dengan Pemilik Rumah saat ini
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == currentOwnerId)
        {
            if (homeLight != null) homeLight.SetActive(true);
            Debug.Log($"<color=yellow>[HomeZone] Efek cahaya dinyalakan untuk Rumah {homeIndex} (Pemilik: ID {currentOwnerId})!</color>");
        }
        else
        {
            if (homeLight != null) homeLight.SetActive(false);
        }
    }

    // FUNGSI PENGAMAN GOAL (PHYSICS)
    private void OnCollisionEnter(Collision collision)
    {
        // Jika server sedang memproses goal, abaikan tumbukan tambahan (Anti-Double Score)
        if (!IsServer || isProcessingGoal) return;

        // Abaikan jika rumah ini belum punya majikan (ID 999)
        if (ZoneOwnerClientId.Value == 999) return;

        if (collision.gameObject.TryGetComponent<ObjectiveItem>(out var objective))
        {
            if (!objective.IsCarried.Value)
            {
                if (collision.gameObject.TryGetComponent<NetworkObject>(out var netObj))
                {
                    if (netObj != null && netObj.IsSpawned)
                    {
                        StartCoroutine(ProcessGoalRoutine(netObj));
                    }
                }
            }
        }
    }

    private IEnumerator ProcessGoalRoutine(NetworkObject ballNetObj)
    {
        isProcessingGoal = true; // Kunci gawang

        Debug.Log($"<color=green>[GOAL!] Bola masuk ke Rumah {homeIndex} (Milik Player ID {ZoneOwnerClientId.Value})!</color>");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(ZoneOwnerClientId.Value, 10);
        }

        if (ObjectiveSpawner.Instance != null)
        {
            ObjectiveSpawner.Instance.DespawnAndRespawnNext(ballNetObj);
        }

        // Tunggu 0.5 detik baru gawang bisa nerima bola lagi (agar sangat aman)
        yield return new WaitForSeconds(0.5f);
        isProcessingGoal = false;
    }
}