using Unity.Netcode;
using UnityEngine;

public class PlayerAnimation : NetworkBehaviour
{
    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;

    // Hash parameter agar performa lebih cepat
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        // Hitung kecepatan gerak horizontal (X dan Z)
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Set parameter Speed ke Animator
        animator.SetFloat(SpeedHash, speed);

        // Cek apakah player lokal sedang membawa objective
        bool isCarrying = CheckIsCarrying();
        animator.SetBool(IsCarryingHash, isCarrying);
    }

    private bool CheckIsCarrying()
    {
        // Cek jika ada ObjectiveItem yang sedang dibawa oleh Player ini
        ObjectiveItem objective = ObjectiveItem.Instance;
        if (objective != null && objective.IsCarried.Value)
        {
            return objective.CarrierClientId.Value == OwnerClientId;
        }
        return false;
    }
}