using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSledding : NetworkBehaviour
{
    [Header("Hitbox Reference")]
    public Collider hitbox;

    [Header("Sledding Settings")]
    [SerializeField] private float slideSpeed = 16f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float slideCooldown = 2f;
    [SerializeField] private float knockbackForce = 12f;

    private Rigidbody rb;
    private PlayerMovement movement;
    private Transform visualModel;

    private bool isSledding = false;
    private bool canSlide = true;
    private Quaternion originalModelRotation;

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
                // 1. LOKAL PREDICTION: Langsung jalankan sledding di layar sendiri tanpa nunggu server
                StartCoroutine(SleddingRoutineLocal(transform.forward));

                // 2. Beritahu Server untuk menjalankan logika bentrokan/tackle
                RequestSleddingServerRpc(transform.forward);
            }
        }
    }

    private bool IsCarryingObjective()
    {
        ObjectiveItem objective = ObjectiveItem.Instance;
        return objective != null && objective.IsCarried.Value && objective.CarrierClientId.Value == OwnerClientId;
    }

    // --- COROUTINE SLEDDING LOKAL (RESPONSIF) ---

    private IEnumerator SleddingRoutineLocal(Vector3 direction)
    {
        isSledding = true;
        canSlide = false;

        if (movement != null) movement.IsSledding = true;
        if (visualModel != null) visualModel.localRotation = Quaternion.Euler(-60f, 0f, 0f);

        float timer = 0f;
        while (timer < slideDuration)
        {
            rb.linearVelocity = new Vector3(direction.x * slideSpeed, rb.linearVelocity.y, direction.z * slideSpeed);
            timer += Time.deltaTime;
            yield return null;
        }

        if (movement != null) movement.IsSledding = false;
        if (visualModel != null) visualModel.localRotation = originalModelRotation;

        isSledding = false;

        yield return new WaitForSeconds(slideCooldown);
        canSlide = true;
    }

    // --- SERVER RPC (LOGIKA TACKLE & HITBOX SERVER) ---

    [ServerRpc]
    private void RequestSleddingServerRpc(Vector3 direction)
    {
        StartCoroutine(SleddingRoutineServer(direction));
        ToggleSleddingVisualClientRpc(true);
    }

    private IEnumerator SleddingRoutineServer(Vector3 direction)
    {
        if (hitbox != null) hitbox.enabled = true;

        // Jika dipanggil di Server (bukan Owner lokal), jalankan gerak fisiknya di Server
        if (!IsOwner)
        {
            if (movement != null) movement.IsSledding = true;
            float timer = 0f;
            while (timer < slideDuration)
            {
                rb.linearVelocity = new Vector3(direction.x * slideSpeed, rb.linearVelocity.y, direction.z * slideSpeed);
                timer += Time.deltaTime;
                yield return null;
            }
            if (movement != null) movement.IsSledding = false;
        }
        else
        {
            yield return new WaitForSeconds(slideDuration);
        }

        if (hitbox != null) hitbox.enabled = false;
        ToggleSleddingVisualClientRpc(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<NetworkObject>(out var targetNetObj))
        {
            // 1. Abaikan jika menabrak diri sendiri
            if (targetNetObj.OwnerClientId == OwnerClientId) return;

            // --- EFEK KNOCKBACK (BERLAKU UNTUK SEMUA PLAYER YANG DITACKLE) ---
            if (other.TryGetComponent<Rigidbody>(out var targetRb))
            {
                // Arah pentalan: Ke depan searah dorongan sledding + sedikit melayang ke atas
                Vector3 knockDir = (targetNetObj.transform.position - transform.position).normalized + Vector3.up * 0.35f;

                // Reset kecepatan korban terlebih dahulu agar pentalan konsisten
                targetRb.linearVelocity = Vector3.zero;
                targetRb.angularVelocity = Vector3.zero;

                // Terapkan gaya pentalan
                targetRb.AddForce(knockDir * knockbackForce, ForceMode.Impulse);
            }

            // Matikan bentrokan fisik fisik antar-collider sementara agar penackle tidak tersangkut/memantul
            IgnorePlayerCollisions(targetNetObj.gameObject);
            rb.linearVelocity = new Vector3(transform.forward.x * slideSpeed, rb.linearVelocity.y, transform.forward.z * slideSpeed);


            // --- EFEK DROP OBJECTIVE (HANYA JIKA TARGET SEDANG MEMBAWA BOLA) ---
            ObjectiveItem objective = ObjectiveItem.Instance;

            if (objective != null && objective.IsCarried.Value && objective.CarrierClientId.Value == targetNetObj.OwnerClientId)
            {
                // Arah lemparan bola: Ke depan + Ke atas agar terlihat melayang indah saat terlepas
                Vector3 objectiveDirection = transform.forward * 0.8f + Vector3.up * 0.6f;

                // Menaikkan multiplier ke 0.5f agar lontaran bola terasa pas (tidak terlalu jauh, tidak terlalu lembek)
                objective.Drop(objectiveDirection, true, 0.5f);

                Debug.Log($"[Sledding Tackle Hit!] Player {OwnerClientId} menackle Player {targetNetObj.OwnerClientId} & Objective terlepas!");
            }
            else
            {
                Debug.Log($"[Sledding Tackle Hit!] Player {OwnerClientId} menackle Player {targetNetObj.OwnerClientId} (Tanpa Objective)!");
            }
        }
    }

    private void IgnorePlayerCollisions(GameObject target)
    {
        Collider[] attackerColliders = GetComponentsInChildren<Collider>();
        Collider[] targetColliders = target.GetComponentsInChildren<Collider>();

        foreach (Collider attackerCollider in attackerColliders)
        {
            foreach (Collider targetCollider in targetColliders)
            {
                Physics.IgnoreCollision(attackerCollider, targetCollider, true);
            }
        }

        StartCoroutine(RestorePlayerCollisions(attackerColliders, targetColliders));
    }

    private IEnumerator RestorePlayerCollisions(Collider[] attackerColliders, Collider[] targetColliders)
    {
        yield return new WaitForSeconds(0.25f);

        foreach (Collider attackerCollider in attackerColliders)
        {
            foreach (Collider targetCollider in targetColliders)
            {
                if (attackerCollider != null && targetCollider != null)
                {
                    Physics.IgnoreCollision(attackerCollider, targetCollider, false);
                }
            }
        }
    }

    [ClientRpc]
    private void ToggleSleddingVisualClientRpc(bool active)
    {
        // Untuk player lain yang melihat (Non-Owner), sesuaikan rotasi visualnya
        if (!IsOwner && visualModel != null)
        {
            if (movement != null) movement.IsSledding = active;
            visualModel.localRotation = active ? Quaternion.Euler(-60f, 0f, 0f) : originalModelRotation;
        }
    }
}