using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;

    private void Awake()
    {
        // Jalankan sebagai Host (Server + Player)
        hostBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            HideUI();
        });

        // Jalankan sebagai Client saja
        clientBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            HideUI();
        });
    }

    private void HideUI()
    {
        hostBtn.gameObject.SetActive(false);
        clientBtn.gameObject.SetActive(false);
    }
}