using UnityEngine;
using UnityEngine.UI;

public class MobileControlPanel : MonoBehaviour
{
    [SerializeField] private GameObject joystickControl;
    [SerializeField] private Button pickupButton;
    [SerializeField] private Button throwButton;
    [SerializeField] private Button sleddingButton;
    [SerializeField] private Button jumpButton;

    private PlayerMechanics ownerMechanics;
    private PlayerSledding ownerSledding;
    private PlayerMovement ownerMovement;

    private void Awake()
    {
        bool supportsMobileControls = Application.isMobilePlatform;
        SetActive(joystickControl, supportsMobileControls);
        SetActive(pickupButton != null ? pickupButton.gameObject : null, false);
        SetActive(throwButton != null ? throwButton.gameObject : null, false);
        SetActive(sleddingButton != null ? sleddingButton.gameObject : null, false);
        SetActive(jumpButton != null ? jumpButton.gameObject : null, false);

        if (!supportsMobileControls)
            return;

        if (joystickControl == null
            || pickupButton == null
            || throwButton == null
            || sleddingButton == null
            || jumpButton == null)
        {
            Debug.LogError("[MobileControlPanel] Assign the joystick and all mobile buttons in the Inspector.", this);
            enabled = false;
            return;
        }

        pickupButton.onClick.AddListener(OnPickupPressed);
        throwButton.onClick.AddListener(OnThrowPressed);
        sleddingButton.onClick.AddListener(OnSleddingPressed);
        jumpButton.onClick.AddListener(OnJumpPressed);
    }

    private void OnDestroy()
    {
        if (pickupButton != null) pickupButton.onClick.RemoveListener(OnPickupPressed);
        if (throwButton != null) throwButton.onClick.RemoveListener(OnThrowPressed);
        if (sleddingButton != null) sleddingButton.onClick.RemoveListener(OnSleddingPressed);
        if (jumpButton != null) jumpButton.onClick.RemoveListener(OnJumpPressed);
    }

    private void Update()
    {
        if (!Application.isMobilePlatform)
            return;

        bool gameplayActive = GameManager.Instance != null
            && GameManager.Instance.IsGameActive.Value
            && !GameManager.Instance.IsIntroPlaying;

        if (!gameplayActive)
        {
            SetActive(joystickControl, false);
            SetActive(pickupButton.gameObject, false);
            SetActive(throwButton.gameObject, false);
            SetActive(sleddingButton.gameObject, false);
            SetActive(jumpButton.gameObject, false);
            return;
        }

        FindOwnerPlayer();

        bool hasOwnerPlayer = ownerMechanics != null && ownerMechanics.IsSpawned;
        SetActive(joystickControl, hasOwnerPlayer);
        SetActive(pickupButton.gameObject, hasOwnerPlayer && ownerMechanics.CanPickUpNearbyObjective);
        SetActive(throwButton.gameObject, hasOwnerPlayer && ownerMechanics.IsCarryingObjective);
        SetActive(sleddingButton.gameObject, hasOwnerPlayer && ownerSledding != null);
        SetActive(jumpButton.gameObject, hasOwnerPlayer && ownerMovement != null);

        if (ownerSledding != null)
            sleddingButton.interactable = ownerSledding.CanStartSledding;
    }

    private void FindOwnerPlayer()
    {
        if (ownerMechanics != null && ownerMechanics.IsSpawned && ownerMechanics.IsOwner)
            return;

        ownerMechanics = null;
        ownerSledding = null;
        ownerMovement = null;

        PlayerMechanics[] players = FindObjectsByType<PlayerMechanics>(FindObjectsInactive.Exclude);
        foreach (PlayerMechanics player in players)
        {
            if (!player.IsOwner || !player.IsSpawned)
                continue;

            ownerMechanics = player;
            ownerSledding = player.GetComponent<PlayerSledding>();
            ownerMovement = player.GetComponent<PlayerMovement>();
            return;
        }
    }

    private void OnPickupPressed()
    {
        if (ownerMechanics != null)
            ownerMechanics.PickUpObjectiveFromMobile();
    }

    private void OnThrowPressed()
    {
        if (ownerMechanics != null)
            ownerMechanics.ThrowObjectiveFromMobile();
    }

    private void OnSleddingPressed()
    {
        if (ownerSledding != null)
            ownerSledding.StartSledding();
    }

    private void OnJumpPressed()
    {
        if (ownerMovement != null)
            ownerMovement.JumpFromMobile();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}