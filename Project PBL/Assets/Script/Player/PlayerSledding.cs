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

    [Header("Tackle Effect")]
    [SerializeField] private float tackleForwardForce = 12f;
    [SerializeField] private float tackleUpwardForce = 5f;
    [SerializeField] private float tackleSpinForce = 8f;
    [SerializeField] private float tackleEffectDuration = 0.75f;

    private Rigidbody rb;
    private PlayerMovement movement;
    private Transform visualModel;

    private bool isSledding = false;
    private bool canSlide = true;
    private bool isBeingTackled = false;
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
        StartCoroutine(IgnoreObjectiveCollisionsForSlide());
        StartCoroutine(IgnorePlayerCollisionsForSlide());

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
        // 1. Nyalakan hitbox tackle di server untuk cek tabrakan
        if (hitbox != null) hitbox.enabled = true;

        // 2. Kunci: JANGAN ubah rb.linearVelocity di Server jika ini bukan Owner!
        // Gerakan fisik sudah ditangani dengan mulus oleh SleddingRoutineLocal di layar Owner,
        // lalu posisinya disinkronkan ke server via NetworkTransform.
        yield return new WaitForSeconds(slideDuration);

        // 3. Matikan hitbox setelah durasi slide selesai
        if (hitbox != null) hitbox.enabled = false;
        ToggleSleddingVisualClientRpc(false);
    }

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
        PlayerSledding[] otherPlayers = FindObjectsByType<PlayerSledding>();
        var ignoredCollisions = new System.Collections.Generic.List<(Collider, Collider)>();

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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<NetworkObject>(out var targetNetObj))
        {
            // 1. Abaikan jika menabrak diri sendiri
            if (targetNetObj.OwnerClientId == OwnerClientId) return;

            PlayerSledding targetSledding = targetNetObj.GetComponent<PlayerSledding>();
            if (targetSledding != null)
            {
                targetSledding.ApplyTackleEffect(transform.forward);
            }

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

    public void ApplyTackleEffect(Vector3 direction)
    {
        if (!IsServer || isBeingTackled) return;

        isBeingTackled = true;
        StartCoroutine(TackleEffectRoutine(direction));

        if (OwnerClientId != NetworkManager.ServerClientId)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
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
        if (movement != null) movement.IsSledding = true;

        rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX |
                            RigidbodyConstraints.FreezeRotationY |
                            RigidbodyConstraints.FreezeRotationZ);

        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        rb.AddForce(forward * tackleForwardForce + Vector3.up * tackleUpwardForce, ForceMode.Impulse);
        Vector3 spinTorque = transform.right + transform.up + transform.forward;
        rb.AddTorque(spinTorque * tackleSpinForce, ForceMode.Impulse);

        yield return new WaitForSeconds(tackleEffectDuration);

        float yaw = rb.rotation.eulerAngles.y;
//        rb.angularVelocity = Vector3.zero;
        rb.rotation = Quaternion.Euler(0f, yaw, 0f);
        rb.constraints = originalConstraints;

        if (movement != null) movement.IsSledding = false;
        isBeingTackled = false;
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