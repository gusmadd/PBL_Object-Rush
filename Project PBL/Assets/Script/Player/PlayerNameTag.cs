using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform canvasTransform;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        new FixedString32Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Transform mainCameraTransform;

    private void Awake()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        PlayerName.OnValueChanged += OnNameChanged;

        // Tampilkan nama saat ini
        UpdateNameDisplay(PlayerName.Value.ToString());

        if (NetworkUI.Instance != null)
        {
            NetworkUI.Instance.RegisterPlayerName(OwnerClientId, PlayerName.Value.ToString());
        }

        // Jika player ini milik client lokal, set namanya dari NetworkUI.LocalUsername
        if (IsOwner)
        {
            SetPlayerName(NetworkUI.LocalUsername);
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnNameChanged;

        if (NetworkUI.Instance != null)
        {
            NetworkUI.Instance.RemovePlayerName(OwnerClientId);
        }
    }

    private void Update()
    {
        // Billboard effect ke kamera
        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        if (mainCameraTransform != null && canvasTransform != null)
        {
            canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - mainCameraTransform.position);
        }
    }

    public void SetPlayerName(string newName)
    {
        if (IsOwner)
        {
            PlayerName.Value = new FixedString32Bytes(newName);
        }
    }

    private void OnNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        UpdateNameDisplay(newValue.ToString());

        // Informasikan ke Room Lobby bahwa nama player telah diperbarui
        if (NetworkUI.Instance != null)
        {
            NetworkUI.Instance.RegisterPlayerName(OwnerClientId, newValue.ToString());
        }
    }

    private void UpdateNameDisplay(string nameToDisplay)
    {
        if (nameText != null)
        {
            nameText.text = nameToDisplay;
        }
    }
}