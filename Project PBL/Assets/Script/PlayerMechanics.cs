using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMechanics : NetworkBehaviour
{
    [Header("Pickup & Drop Settings")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private Transform carryPoint;

    public Transform GetCarryPoint() => carryPoint;

    private void Update()
    {
        if (!IsOwner) return;

        // Tekan 'E' untuk Ambil / Lempar Item
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            RequestToggleItemServerRpc();
        }
    }

    [ServerRpc]
    private void RequestToggleItemServerRpc()
    {
        ObjectiveItem objective = ObjectiveItem.Instance;
        if (objective == null) return;

        // Jika player ini sedang membawa item -> Lempar!
        if (objective.IsCarried.Value && objective.CarrierClientId.Value == OwnerClientId)
        {
            objective.Drop(transform.forward);
            Debug.Log($"[Server] Player {OwnerClientId} melempar item!");
        }
        // Jika item sedang bebas -> Ambil!
        else if (!objective.IsCarried.Value)
        {
            float distance = Vector3.Distance(transform.position, objective.transform.position);
            if (distance <= pickupRange)
            {
                objective.PickUp(OwnerClientId, carryPoint);
                Debug.Log($"[Server] Player {OwnerClientId} mengambil item!");
            }
        }
    }
}