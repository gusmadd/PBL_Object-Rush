using Unity.Netcode;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    void Start()
    {
        // otomatis jadi Host saat game dijalankan
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Host Started");
        }
        else
        {
            Debug.LogError("NetworkManager tidak ditemukan!");
        }
    }
}