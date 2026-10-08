using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private ParticleSystem runningParticles;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private Transform mainCameraTransform;
    private CapsuleCollider capsuleCollider;
    private bool isGrounded;
    private Vector3 moveInput;
    private float jumpBufferCounter;
    private float coyoteCounter;
    private bool runParticleActive;
    public bool IsGrounded => isGrounded;
    public bool IsSledding { get; set; } = false;

    [ClientRpc]
    public void TeleportOwnerClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        if (TryGetComponent<NetworkTransform>(out var netTransform))
        {
            if (!netTransform.CanCommitToTransform)
            {
                Debug.LogError($"[PlayerMovement] Owner {OwnerClientId} tidak berwenang mengubah transform.");
                return;
            }

            netTransform.Teleport(position, rotation, transform.localScale);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        if (TryGetComponent<Rigidbody>(out var playerRigidbody))
        {
            playerRigidbody.position = position;
            playerRigidbody.rotation = rotation;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[PlayerMovement] Owner {OwnerClientId} selesai teleport ke {transform.position}.");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (runningParticles != null)
            runningParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (!IsOwner || !Application.isFocused || Keyboard.current == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsIntroPlaying) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameActive.Value)
        {
            // Saat Game Berjalan: Kunci kursor untuk mengontrol kamera
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Saat di Menu / Lobby: Tampilkan kursor agar bisa klik UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Jangan proses pergerakan player jika game belum dimulai
            return;
        }

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !Application.isFocused)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsIntroPlaying) return;

        CheckGrounded();

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }

        jumpBufferCounter -= Time.fixedDeltaTime;

        bool isRunning = !IsSledding && isGrounded && moveInput.sqrMagnitude > 0.01f;
        if (isRunning != runParticleActive)
        {
            runParticleActive = isRunning;
            SetRunningParticleServerRpc(isRunning);
        }

        if (IsSledding) return;

        HandleMovementInput();
        HandleJumpInput();
    }
    [ServerRpc]
    private void SetRunningParticleServerRpc(bool isRunning)
    {
        SetRunningParticleClientRpc(isRunning);
    }

    [ClientRpc]
    private void SetRunningParticleClientRpc(bool isRunning)
    {
        if (runningParticles == null) return;

        if (isRunning)
            runningParticles.Play();
        else
            runningParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void CheckGrounded()
    {
        Bounds bounds = capsuleCollider.bounds;
        Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
        int layerMask = groundLayer.value != 0 ? groundLayer.value : Physics.DefaultRaycastLayers;
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance + 0.05f, layerMask);

        Debug.DrawRay(rayOrigin, Vector3.down * (groundCheckDistance + 0.05f), isGrounded ? Color.green : Color.red);
    }

    private void HandleMovementInput()
    {
        if (moveInput.magnitude <= 0.1f)
        {
            rb.angularVelocity = Vector3.zero;
            return;
        }

        float targetAngle = Mathf.Atan2(moveInput.x, moveInput.z) * Mathf.Rad2Deg;

        if (mainCameraTransform != null)
        {
            targetAngle += mainCameraTransform.eulerAngles.y;
        }

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // Gunakan FixedUpdate agar pergerakan Rigidbody lebih stabil dan tidak geter seperti Update.
        Vector3 targetVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));
        rb.angularVelocity = Vector3.zero;
    }

    private void HandleJumpInput()
    {
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
            return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Deadzone"))
        {
            RequestRespawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Deadzone"))
        {
            RequestRespawn();
        }
    }

    private void RequestRespawn()
    {
        if (IsServer && PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.RespawnPlayer(OwnerClientId);
        }
    }
}
