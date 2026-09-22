using Unity.Netcode;
using UnityEngine;

public class ObjectiveItem : NetworkBehaviour
{
    public static ObjectiveItem Instance { get; private set; }

    [Header("Network States")]
    public NetworkVariable<bool> IsCarried = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> CarrierClientId = new NetworkVariable<ulong>(999);

    [Header("Throw Settings")]
    [SerializeField] private float throwForceForward = 8f;
    [SerializeField] private float throwForceUp = 3f;

    private Rigidbody rb;
    private Collider col;
    private NetworkObject netObj;
    private Transform currentCarrierPoint;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        netObj = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        // Secara visual, kunci posisi item ke CarryPoint pembawa di semua client
        if (IsCarried.Value && currentCarrierPoint != null)
        {
            transform.position = currentCarrierPoint.position;
            transform.rotation = currentCarrierPoint.rotation;
        }
    }

    // --- LOGIKA SERVER ---

    public void PickUp(ulong clientId, Transform playerCarryPoint)
    {
        if (!IsServer) return;

        CarrierClientId.Value = clientId;
        IsCarried.Value = true;

        SetCarrierClientRpc(clientId);
    }

    public void Drop(Vector3 throwDirection)
    {
        if (!IsServer) return;

        IsCarried.Value = false;
        CarrierClientId.Value = 999;

        // Fisika & Collider diaktifkan kembali di Server
        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;

            // Hitung gaya lemparan
            Vector3 finalThrowForce = (throwDirection.normalized * throwForceForward) + (Vector3.up * throwForceUp);
            rb.AddForce(finalThrowForce, ForceMode.Impulse);
        }

        // Kabarkan seluruh Client untuk melepas ikatan visual
        DropClientRpc();
    }

    // --- CLIENT RPCS (VISUAL & LOCAL STATE) ---

    [ClientRpc]
    private void SetCarrierClientRpc(ulong clientId)
    {
        // Matikan collider lokal agar tidak bentrok dengan player
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var c in allColliders)
        {
            c.enabled = false;
        }

        if (rb != null) rb.isKinematic = true;

        // Cari Transform CarryPoint milik Player penerima
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(GetPlayerNetworkObjectId(clientId), out var playerNetObj))
        {
            PlayerMechanics mechanics = playerNetObj.GetComponent<PlayerMechanics>();
            if (mechanics != null && mechanics.GetCarryPoint() != null)
            {
                currentCarrierPoint = mechanics.GetCarryPoint();
            }
            else
            {
                currentCarrierPoint = playerNetObj.transform;
            }
        }
    }

    [ClientRpc]
    private void DropClientRpc()
    {
        currentCarrierPoint = null;

        if (rb != null) rb.isKinematic = false;

        // Aktifkan kembali collider di semua client
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var c in allColliders)
        {
            c.enabled = true;
        }
    }

    private ulong GetPlayerNetworkObjectId(ulong clientId)
    {
        var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        return playerObj != null ? playerObj.NetworkObjectId : 0;
    }
}