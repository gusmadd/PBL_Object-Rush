using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform canvasTransform;

    // NetworkVariable untuk menyinkronkan nama player ke semua client secara otomatis
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        new FixedString32Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner // Owner boleh mengubah namanya sendiri
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
        // Subscribe event: Jika variabel nama berubah, update tampilan UI Text
        PlayerName.OnValueChanged += OnNameChanged;

        // Tampilkan nama awal
        UpdateNameDisplay(PlayerName.Value.ToString());

        // Contoh: Set nama default saat pertama kali spawn jika belum diset
        if (IsOwner)
        {
            // Nanti nilai ini bisa diambil dari input UI Nama buatan Sultan
            SetPlayerName($"Player {OwnerClientId + 1}");
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnNameChanged;
    }

    private void Update()
    {
        // Billboard effect: Bikin UI Text Nama selalu menghadap ke Kamera
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
    }

    private void UpdateNameDisplay(string nameToDisplay)
    {
        if (nameText != null)
        {
            nameText.text = nameToDisplay;
        }
    }
}