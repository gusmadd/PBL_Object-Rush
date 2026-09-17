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
    private Vector3 moveInput;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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
        jumpRequested = Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded;
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !Application.isFocused)
            return;

        CheckGrounded();
        HandleMovementInput();
        HandleJumpInput();
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
        if (!jumpRequested || !isGrounded)
            return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpRequested = false;
    }
}
