using Unity.Netcode;
using UnityEngine;

public class ObjectiveItem : NetworkBehaviour
{
    public static ObjectiveItem Instance { get; private set; }

    [Header("Network States")]
    public NetworkVariable<bool> IsCarried = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> CarrierClientId = new NetworkVariable<ulong>(999);
    public NetworkVariable<bool> CanBePickedUp = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> HasTouchedGround = new NetworkVariable<bool>(false);

    [Header("Throw Settings")]
    [SerializeField] private float throwForceForward = 8f;
    [SerializeField] private float throwForceUp = 3f;
    [SerializeField] private float spawnBounceImpulse = 1f;

    private Rigidbody rb;
    private Collider col;
    private NetworkObject netObj;
    private Transform currentCarrierPoint;
    private bool waitingForLanding;
    private bool hasBouncedOnSpawn;
    private float droppedAt;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        netObj = GetComponent<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        HasTouchedGround.OnValueChanged += OnHasTouchedGroundChanged;
        if (HasTouchedGround.Value)
        {
            DisableObjectiveLights();
        }
    }

    public override void OnNetworkDespawn()
    {
        HasTouchedGround.OnValueChanged -= OnHasTouchedGroundChanged;
    }

    [Header("Fall Respawn Settings")]
    [SerializeField] private float fallYThreshold = -5f; // Batas posisi Y terendah sebelum despawn

    private void Update()
    {
        // 1. Terapkan posisi jika sedang dibawa player
        if (IsCarried.Value && currentCarrierPoint != null)
        {
            transform.position = currentCarrierPoint.position;
            transform.rotation = currentCarrierPoint.rotation;
        }

        // 2. Cek apakah bola jatuh melebihi batas Y (Hanya dieksekusi oleh Server)
        if (IsServer && transform.position.y < fallYThreshold)
        {
            if (ObjectiveSpawner.Instance != null && IsSpawned)
            {
                Debug.Log($"<color=orange>[ObjectiveItem] Bola jatuh di Y: {transform.position.y}! Despawn & Respawn...</color>");
                ObjectiveSpawner.Instance.DespawnAndRespawnNext(GetComponent<NetworkObject>());
            }
        }
    }

    private void LateUpdate()
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
        if (!CanBePickedUp.Value) return;

        NetworkObject playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObject == null) return;

        CarrierClientId.Value = clientId;
        IsCarried.Value = true;
        waitingForLanding = false;

        SetCarrierClientRpc(playerObject.NetworkObjectId);
    }

    public void Drop(Vector3 throwDirection, bool applyThrowForce = true, float throwForceMultiplier = 1f)
    {
        if (!IsServer || !IsCarried.Value) return;

        IsCarried.Value = false;
        CarrierClientId.Value = 999;
        CanBePickedUp.Value = false;
        waitingForLanding = true;
        droppedAt = Time.time;

        // Fisika & Collider diaktifkan kembali di Server
        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (applyThrowForce)
            {
                Vector3 finalThrowForce = ((throwDirection.normalized * throwForceForward) + (Vector3.up * throwForceUp)) * throwForceMultiplier;
                rb.AddForce(finalThrowForce, ForceMode.Impulse);
            }
        }

        // Kabarkan seluruh Client untuk melepas ikatan visual
        DropClientRpc();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || collision.rigidbody != null || (waitingForLanding && Time.time < droppedAt + 0.1f))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                if (!HasTouchedGround.Value)
                {
                    HasTouchedGround.Value = true;
                }

                if (!hasBouncedOnSpawn && rb != null)
                {
                    rb.AddForce(Vector3.up * spawnBounceImpulse, ForceMode.Impulse);
                    hasBouncedOnSpawn = true;
                }

                if (waitingForLanding)
                {
                    waitingForLanding = false;
                    CanBePickedUp.Value = true;
                }
                break;
            }
        }
        
        if (collision.gameObject.CompareTag("Deadzone"))
        {
            if (ObjectiveSpawner.Instance != null && IsSpawned)
            {
                Debug.Log("<color=red>[ObjectiveItem] Menyentuh Deadzone Plane! Despawn & Respawn...</color>");
                ObjectiveSpawner.Instance.DespawnAndRespawnNext(GetComponent<NetworkObject>());
            }
        }
    }

    // --- CLIENT RPCS (VISUAL & LOCAL STATE) ---

    private void OnHasTouchedGroundChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            DisableObjectiveLights();
        }
    }

    private void DisableObjectiveLights()
    {
        Light[] objectiveLights = GetComponentsInChildren<Light>(true);
        foreach (Light objectiveLight in objectiveLights)
        {
            objectiveLight.enabled = false;
        }
    }

    [ClientRpc]
    private void SetCarrierClientRpc(ulong playerNetworkObjectId)
    {
        // Matikan collider lokal agar tidak bentrok dengan player
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var c in allColliders)
        {
            c.enabled = false;
        }

        if (rb != null) rb.isKinematic = true;

        // Cari Transform CarryPoint milik Player penerima
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerNetObj))
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

}