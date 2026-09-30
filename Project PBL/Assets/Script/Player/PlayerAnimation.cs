using Unity.Netcode;
using UnityEngine;

public class PlayerAnimation : NetworkBehaviour
{
    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlayerMovement movement;

    // Hash parameter Animator agar lebih efisien
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");
    private static readonly int IsSleddingHash = Animator.StringToHash("IsSledding");
    private static readonly int ThrowHash = Animator.StringToHash("Throw");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        // 1. Movement Speed Threshold
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float rawSpeed = horizontalVelocity.magnitude;
        float smoothedSpeed = rawSpeed < 0.1f ? 0f : rawSpeed;
        animator.SetFloat(SpeedHash, smoothedSpeed);

        // 2. IsCarrying State
        bool isCarrying = CheckIsCarrying();
        animator.SetBool(IsCarryingHash, isCarrying);

        // 3. IsSledding State
        if (movement != null)
        {
            animator.SetBool(IsSleddingHash, movement.IsSledding);
        }
    }

    // Fungsi Publik untuk mentrigger Animasi Lempar (Dipanggil saat player menekan tombol lempar)
    public void TriggerThrowAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(ThrowHash);
        }
    }

    private bool CheckIsCarrying()
    {
        // Cari apakah ada ObjectiveItem yang sedang dibawa oleh Player ini
        ObjectiveItem[] allObjectives = FindObjectsByType<ObjectiveItem>();
        foreach (var item in allObjectives)
        {
            if (item.IsCarried.Value && item.CarrierClientId.Value == OwnerClientId)
            {
                return true;
            }
        }
        return false;
    }
}