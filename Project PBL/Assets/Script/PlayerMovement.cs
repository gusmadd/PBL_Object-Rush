using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private Transform mainCameraTransform;
    private CapsuleCollider capsuleCollider;
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void Start()
    {
        // Simpan referensi Main Camera untuk kalkulasi arah jalan berbasis POV Kamera
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        // PENTING: Input & pergerakan hanya dihitung di client lokal yang memiliki Player ini!
        //    if (!IsOwner) return;

        CheckGrounded();
        HandleMovementInput();
        HandleJumpInput();
    }

    private void CheckGrounded()
    {
        // Mulai pengecekan dari bawah collider, bukan dari pivot player.
        Bounds bounds = capsuleCollider.bounds;
        Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
        int layerMask = groundLayer.value != 0 ? groundLayer.value : Physics.DefaultRaycastLayers;
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance + 0.05f, layerMask);

        // Visual debug garis di Scene View (Hijau = di tanah, Merah = di udara)
        Debug.DrawRay(rayOrigin, Vector3.down * (groundCheckDistance + 0.05f), isGrounded ? Color.green : Color.red);
    }

    private void HandleMovementInput()
    {
        if (Keyboard.current == null) return;

        // BACA INPUT DARI KEYBOARD (NEW INPUT SYSTEM)
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;

            if (mainCameraTransform != null)
            {
                targetAngle += mainCameraTransform.eulerAngles.y;
            }

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
    private void HandleJumpInput()
    {
        if (Keyboard.current == null) return;

        // Tekan Tombol Spasi untuk Lompat saat karakter berada di tanah
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            // Reset Kecepatan Y sebelum lompat agar lompatan konsisten
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // Tambahkan dorongan ke atas
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}
