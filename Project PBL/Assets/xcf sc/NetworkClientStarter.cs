using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkClientStarter : MonoBehaviour
{
    void Start()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            "127.0.0.1", // ganti IP host kalau beda PC
            7777
        );

        NetworkManager.Singleton.StartClient();
    }
}