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
        // 1. Cek apakah Player sedang membawa item apapun
        ObjectiveItem carriedObjective = GetCarriedObjective();

        if (carriedObjective != null)
        {
            // Lempar Item!
            carriedObjective.Drop(transform.forward);
            Debug.Log($"[Server] Player {OwnerClientId} melempar item!");
        }
        else
        {
            // 2. Jika tidak membawa item, cari ObjectiveItem bebas di sekitar Player
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (var hit in hitColliders)
            {
                if (hit.TryGetComponent<ObjectiveItem>(out var nearbyObjective))
                {
                    if (!nearbyObjective.IsCarried.Value && nearbyObjective.CanBePickedUp.Value)
                    {
                        nearbyObjective.PickUp(OwnerClientId, carryPoint);
                        Debug.Log($"[Server] Player {OwnerClientId} mengambil item!");
                        break;
                    }
                }
            }
        }
    }

    private ObjectiveItem GetCarriedObjective()
    {
        // Cukup gunakan FindObjectsByType<ObjectiveItem>() tanpa parameter SortMode di Unity 6
        ObjectiveItem[] allObjectives = FindObjectsByType<ObjectiveItem>();

        foreach (var item in allObjectives)
        {
            if (item.IsCarried.Value && item.CarrierClientId.Value == OwnerClientId)
            {
                return item;
            }
        }
        return null;
    }
}