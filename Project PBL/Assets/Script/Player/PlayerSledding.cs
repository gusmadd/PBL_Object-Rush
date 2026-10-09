using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSledding : NetworkBehaviour
{
    private readonly NetworkVariable<bool> isSleddingNetworked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    [Header("Hitbox Reference")]
    public Collider hitbox;

    [Header("Sledding Settings")]
    [SerializeField] private float slideSpeed = 16f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideCooldown = 2f;
    [SerializeField] private ParticleSystem sleddingParticles;

    [Header("Tackle Effect")]
    [SerializeField] private float tackleForwardForce = 12f;
    [SerializeField] private float tackleUpwardForce = 5f;
    [SerializeField] private float tackleSpinForce = 8f;
    [SerializeField] private float tackleEffectDuration = 0.75f;

    private Rigidbody rb;
    private PlayerMovement movement;
    private Transform visualModel;

    private bool isSledding = false;
    private bool isSleddingOnServer;
    private bool canSlide = true;
    private bool isBeingTackled = false;
    private Quaternion originalModelRotation;

    public bool IsSledding => isSledding || isSleddingOnServer || isSleddingNetworked.Value;
    public bool IsBeingTackled => isBeingTackled;

    // Menyimpan daftar korban yang sudah ditabrak dalam 1 kali tarikan sledding
    private HashSet<ulong> hitClients = new HashSet<ulong>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();

        if (transform.childCount > 0)
        {
            visualModel = transform.GetChild(0);
            originalModelRotation = visualModel.localRotation;
        }

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            if (canSlide && !isSledding && !IsCarryingObjective() && movement != null && movement.IsGrounded)
            {
                // 1. Langsung jalankan gerakan sledding dan nyalakan hitbox di layar sendiri (Client-Side)
                StartCoroutine(SleddingRoutineLocal(transform.forward));

                // 2. Lapor ke Server untuk memunculkan efek visual/partikel ke player lain
                RequestSleddingServerRpc(transform.forward);
            }
        }
    }

    private bool IsCarryingObjective()
    {
        ObjectiveItem objective = ObjectiveItem.Instance;
        return objective != null && objective.IsCarried.Value && objective.CarrierClientId.Value == OwnerClientId;
    }

    // --- COROUTINE SLEDDING LOKAL (RESPONSIF & HITBOX AKTIF) ---

    private IEnumerator SleddingRoutineLocal(Vector3 direction)
    {
        isSledding = true;
        canSlide = false;
        hitClients.Clear(); // Bersihkan daftar korban

        // NYALAKAN HITBOX DI LOKAL AGAR BISA MENDETEKSI TABRAKAN SECARA INSTAN
        if (hitbox != null) hitbox.enabled = true;

        SetSleddingParticles(true);
        StartCoroutine(IgnoreObjectiveCollisionsForSlide());
        StartCoroutine(IgnorePlayerCollisionsForSlide());

        SetSleddingState(true);
        if (visualModel != null) visualModel.localRotation = GetSleddingModelRotation();

        float timer = 0f;
        while (timer < slideDuration)
        {
            rb.linearVelocity = new Vector3(direction.x * slideSpeed, rb.linearVelocity.y, direction.z * slideSpeed);
            timer += Time.deltaTime;
            yield return null;
        }

        SetSleddingState(false);
        if (visualModel != null) visualModel.localRotation = originalModelRotation;

        // MATIKAN HITBOX DI LOKAL SETELAH SELESAI SLEDDING
        if (hitbox != null) hitbox.enabled = false;

        SetSleddingParticles(false);
        isSledding = false;

        InGameHUDManager.Instance?.SetSleddingCooldown(0f);
        float cooldownTimer = 0f;
        while (cooldownTimer < slideCooldown)
        {
            cooldownTimer += Time.deltaTime;
            InGameHUDManager.Instance?.SetSleddingCooldown(cooldownTimer / slideCooldown);
            yield return null;
        }

        InGameHUDManager.Instance?.HideSleddingCooldown();
        canSlide = true;
    }

    // --- SERVER RPC (VISUAL SYNC) ---

    [ServerRpc]
    private void RequestSleddingServerRpc(Vector3 direction)
    {
        isSleddingOnServer = true;
        StartCoroutine(SleddingRoutineServer(direction));
        ToggleSleddingVisualClientRpc(true);
    }

    private IEnumerator SleddingRoutineServer(Vector3 direction)
    {
        // Server hanya mengatur durasi tayang visual, hitbox fisik tidak perlu diurus server lagi
        yield return new WaitForSeconds(slideDuration);
        isSleddingOnServer = false;
        ToggleSleddingVisualClientRpc(false);
    }

    // --- MANAJEMEN KOLISI (IGNORE) ---

    private IEnumerator IgnoreObjectiveCollisionsForSlide()
    {
        ObjectiveItem objective = ObjectiveItem.Instance;
        if (objective == null) yield break;

        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        Collider[] objectiveColliders = objective.GetComponentsInChildren<Collider>();

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            foreach (Collider playerCollider in playerColliders)
            {
                foreach (Collider objectiveCollider in objectiveColliders)
                {
                    if (playerCollider != null && objectiveCollider != null)
                    {
                        Physics.IgnoreCollision(playerCollider, objectiveCollider, true);
                    }
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        foreach (Collider playerCollider in playerColliders)
        {
            foreach (Collider objectiveCollider in objectiveColliders)
            {
                if (playerCollider != null && objectiveCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, objectiveCollider, false);
                }
            }
        }
    }

    private IEnumerator IgnorePlayerCollisionsForSlide()
    {
        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        PlayerSledding[] otherPlayers = FindObjectsByType<PlayerSledding>(FindObjectsInactive.Exclude);
        var ignoredCollisions = new List<(Collider, Collider)>();

        foreach (PlayerSledding otherPlayer in otherPlayers)
        {
            if (otherPlayer == this) continue;

            Collider[] otherColliders = otherPlayer.GetComponentsInChildren<Collider>();
            foreach (Collider playerCollider in playerColliders)
            {
                foreach (Collider otherCollider in otherColliders)
                {
                    if (playerCollider == null || otherCollider == null) continue;
                    if (playerCollider == hitbox || otherCollider == otherPlayer.hitbox) continue;

                    Physics.IgnoreCollision(playerCollider, otherCollider, true);
                    ignoredCollisions.Add((playerCollider, otherCollider));
                }
            }
        }

        yield return new WaitForSeconds(slideDuration);

        foreach (var collision in ignoredCollisions)
        {
            if (collision.Item1 != null && collision.Item2 != null)
            {
                Physics.IgnoreCollision(collision.Item1, collision.Item2, false);
            }
        }
    }

    // --- CLIENT-SIDE HIT DETECTION ---

    private void OnTriggerEnter(Collider other)
    {
        // 1. HANYA CEK TABRAKAN JIKA INI PLAYER KITA SENDIRI & SEDANG SLEDDING
        if (!IsOwner || !isSledding) return;

        if (other.TryGetComponent<NetworkObject>(out var targetNetObj))
        {
            // Abaikan jika menabrak diri sendiri
            if (targetNetObj.OwnerClientId == OwnerClientId) return;

            // Pastikan korban ini belum ditabrak di 1 tarikan sledding yang sama
            if (!hitClients.Add(targetNetObj.OwnerClientId)) return;

            // 2. LAPOR KE SERVER BAHWA KITA BERHASIL MENABRAK PLAYER INI!
            NotifyTackleHitServerRpc(targetNetObj.OwnerClientId, transform.forward);
        }
    }

    // --- SERVER RPC (EKSEKUSI JATUH & BOLA LEPAS) ---

    [ServerRpc]
    private void NotifyTackleHitServerRpc(ulong targetClientId, Vector3 tackleDirection)
    {
        // 3. SERVER MENERIMA LAPORAN, LALU MEMBUAT KORBAN JATUH DAN BOLA TERLEPAS

        var targetNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetClientId);
        if (targetNetObj == null) return;

        // A. Terapkan efek mental/jatuh ke korban
        PlayerSledding targetSledding = targetNetObj.GetComponent<PlayerSledding>();
        if (targetSledding != null)
        {
            targetSledding.ApplyTackleEffect(tackleDirection);
        }

        // B. Cek apakah korban sedang membawa bola
        ObjectiveItem objective = ObjectiveItem.Instance;
        if (objective != null && objective.IsCarried.Value && objective.CarrierClientId.Value == targetClientId)
        {
            // Bola terlempar serong ke atas
            Vector3 objectiveDirection = tackleDirection * 0.8f + Vector3.up * 0.6f;
            objective.Drop(objectiveDirection, true, 0.5f);

            Debug.Log($"<color=orange>[Sledding Hit!] Player {OwnerClientId} menabrak Player {targetClientId} & Objective terlepas!</color>");
        }
        else
        {
            Debug.Log($"<color=orange>[Sledding Hit!] Player {OwnerClientId} menabrak Player {targetClientId} (Tanpa Objective)!</color>");
        }
    }

    // --- LOGIKA MENTAL / JATUH ---

    public void ApplyTackleEffect(Vector3 direction)
    {
        if (!IsServer || isBeingTackled) return;

        isBeingTackled = true;
        StartCoroutine(TackleEffectRoutine(direction));

        if (OwnerClientId != NetworkManager.ServerClientId)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            ApplyTackleEffectClientRpc(direction, clientRpcParams);
        }
    }

    [ClientRpc]
    private void ApplyTackleEffectClientRpc(Vector3 direction, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner || isBeingTackled) return;

        isBeingTackled = true;
        StartCoroutine(TackleEffectRoutine(direction));
    }

    private IEnumerator TackleEffectRoutine(Vector3 direction)
    {
        RigidbodyConstraints originalConstraints = rb.constraints;
        SetSleddingState(true);

        rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX |
                            RigidbodyConstraints.FreezeRotationY |
                            RigidbodyConstraints.FreezeRotationZ);

        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        rb.AddForce(forward * tackleForwardForce + Vector3.up * tackleUpwardForce, ForceMode.Impulse);
        Vector3 spinTorque = transform.right + transform.up + transform.forward;
        rb.AddTorque(spinTorque * tackleSpinForce, ForceMode.Impulse);

        yield return new WaitForSeconds(tackleEffectDuration);

        float yaw = rb.rotation.eulerAngles.y;
        rb.rotation = Quaternion.Euler(0f, yaw, 0f);
        rb.constraints = originalConstraints;

        SetSleddingState(false);
        isBeingTackled = false;
    }

    // --- VISUAL RPC ---

    private void SetSleddingState(bool active)
    {
        if (movement != null) movement.IsSledding = active;
        if (IsOwner && IsSpawned)
        {
            isSleddingNetworked.Value = active;
        }
    }

    private static Quaternion GetSleddingModelRotation()
    {
        return Quaternion.Euler(0f, -90f, 60f);
    }

    [ClientRpc]
    private void ToggleSleddingVisualClientRpc(bool active)
    {
        SetSleddingParticles(active);

        if (!IsOwner && visualModel != null)
        {
            if (movement != null) movement.IsSledding = active;
            visualModel.localRotation = active ? GetSleddingModelRotation() : originalModelRotation;
        }
    }

    private void SetSleddingParticles(bool active)
    {
        if (sleddingParticles == null) return;

        if (active)
            sleddingParticles.Play();
        else
            sleddingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}