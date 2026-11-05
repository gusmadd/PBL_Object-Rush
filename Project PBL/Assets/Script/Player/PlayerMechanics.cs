using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMechanics : NetworkBehaviour
{
    [Header("Pickup & Drop Settings")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private Transform carryPoint;

    [Header("Pickup Indicator")]
    [SerializeField] private GameObject pickupIndicator;
    [SerializeField] private Image pickupIndicatorImage;
    [SerializeField] private float pickupIndicatorBlinkSpeed = 4f;

    private readonly Collider[] pickupCheckBuffer = new Collider[16];
    private PlayerSledding playerSledding;
    private bool canPickUpNearbyObjective;

    public Transform GetCarryPoint() => carryPoint;
    public bool IsCarryingObjective
    {
        get
        {
            ObjectiveItem objective = ObjectiveItem.Instance;
            return objective != null
                && objective.IsCarried.Value
                && objective.CarrierClientId.Value == OwnerClientId;
        }
    }
    public bool CanPickUpNearbyObjective => canPickUpNearbyObjective;

    private void Awake()
    {
        playerSledding = GetComponent<PlayerSledding>();

        if (pickupIndicator != null)
        {
            pickupIndicator.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdatePickupIndicator();

        // Tekan 'E' untuk Ambil / Lempar Item
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (GetCarriedObjective() == null && IsPickupBlocked())
                return;

            RequestToggleItemServerRpc();
        }
    }

    private bool IsPickupBlocked()
    {
        return playerSledding != null
            && (playerSledding.IsSledding || playerSledding.IsBeingTackled);
    }

    private void UpdatePickupIndicator()
    {
        ObjectiveItem objective = ObjectiveItem.Instance;
        bool canPickUp = !IsPickupBlocked()
            && objective != null
            && !objective.IsCarried.Value
            && objective.CanBePickedUp.Value;

        if (canPickUp)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                pickupRange,
                pickupCheckBuffer
            );

            canPickUp = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = pickupCheckBuffer[i];
                if (hit != null
                    && hit.TryGetComponent<ObjectiveItem>(out ObjectiveItem nearbyObjective)
                    && nearbyObjective == objective
                    && !nearbyObjective.IsCarried.Value
                    && nearbyObjective.CanBePickedUp.Value)
                {
                    canPickUp = true;
                    break;
                }
            }
        }

        canPickUpNearbyObjective = canPickUp;

        if (pickupIndicator != null && pickupIndicator.activeSelf != canPickUp)
        {
            pickupIndicator.SetActive(canPickUp);
        }

        if (canPickUp && pickupIndicatorImage != null)
        {
            float blink = (Mathf.Sin(Time.time * pickupIndicatorBlinkSpeed) + 1f) * 0.5f;
            pickupIndicatorImage.color = Color.Lerp(Color.green, Color.white, blink);
        }
    }

    public void PickUpObjectiveFromMobile()
    {
        if (!IsOwner || IsCarryingObjective || IsPickupBlocked() || !canPickUpNearbyObjective)
            return;

        RequestMobilePickupServerRpc();
    }

    public void ThrowObjectiveFromMobile()
    {
        if (!IsOwner || !IsCarryingObjective)
            return;

        RequestMobileThrowServerRpc();
    }

    [ServerRpc]
    private void RequestToggleItemServerRpc()
    {
        // 1. Cek apakah Player sedang membawa item apapun
        ObjectiveItem carriedObjective = GetCarriedObjective();

        if (carriedObjective != null)
        {
            // Lempar Item!
            PlayThrowAnimationClientRpc();
            carriedObjective.Drop(transform.forward);
            Debug.Log($"[Server] Player {OwnerClientId} melempar item!");
        }
        else
        {
            if (TryPickUpNearbyObjective())
            {
                Debug.Log($"[Server] Player {OwnerClientId} mengambil item!");
            }
        }
    }

    [ServerRpc]
    private void RequestMobilePickupServerRpc()
    {
        if (GetCarriedObjective() != null || IsPickupBlocked())
            return;

        if (TryPickUpNearbyObjective())
        {
            Debug.Log($"[Server] Player {OwnerClientId} mengambil item!");
        }
    }

    [ServerRpc]
    private void RequestMobileThrowServerRpc()
    {
        ObjectiveItem carriedObjective = GetCarriedObjective();
        if (carriedObjective == null)
            return;

        PlayThrowAnimationClientRpc();
        carriedObjective.Drop(transform.forward);
        Debug.Log($"[Server] Player {OwnerClientId} melempar item!");
    }

    private bool TryPickUpNearbyObjective()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider hit in hitColliders)
        {
            if (hit.TryGetComponent<ObjectiveItem>(out ObjectiveItem nearbyObjective)
                && !nearbyObjective.IsCarried.Value
                && nearbyObjective.CanBePickedUp.Value)
            {
                return nearbyObjective.PickUp(OwnerClientId, carryPoint);
            }
        }

        return false;
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

    [ClientRpc]
    private void PlayThrowAnimationClientRpc()
    {
        if (IsOwner)
        {
            SoundManager.Instance?.PlayThrowSfx();
        }

        PlayerAnimation playerAnim = GetComponent<PlayerAnimation>();
        if (playerAnim != null)
        {
            playerAnim.TriggerThrowAnimation();
        }
    }
}