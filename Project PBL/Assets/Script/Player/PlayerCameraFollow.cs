using Unity.Netcode;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerCameraFollow : NetworkBehaviour
{
    [Header("Camera Target")]
    [SerializeField] private Transform lookPointTarget;

    public override void OnNetworkSpawn()
    {
        // Hanya jalankan jika script ini ada di Player milik Client/Host lokal di jendelanya masing-masing
        if (!IsOwner)
        {
            enabled = false;
            return;

        } 
        StartCoroutine(AttachCameraNextFrame());
    }

   private IEnumerator AttachCameraNextFrame()
    {
        yield return null; // Tunggu 1 frame

        CinemachineCamera freeLookCam = FindAnyObjectByType<CinemachineCamera>();

        if (freeLookCam != null)
        {
            Transform target = lookPointTarget != null ? lookPointTarget : transform;

            freeLookCam.Follow = target;
            freeLookCam.LookAt = target;

            Debug.Log($"[Camera Success] Kamera di layar ini resmi mengunci Player ID: {OwnerClientId}");
        }
        else
        {
            Debug.LogError("[Camera Error] Tidak menemukan CinemachineCamera di Scene!");
        }
    }
}