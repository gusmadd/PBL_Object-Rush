using Unity.Netcode;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerCameraFollow : NetworkBehaviour
{
    [Header("Camera Target")]
    [SerializeField] private Transform lookPointTarget;
    private CinemachineOrbitalFollow orbitalFollow;

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

        // CARI KAMERA SECARA SPESIFIK BERDASARKAN NAMA AGAR TIDAK TERTUKAR DENGAN INTRO CAM
        GameObject camObj = GameObject.Find("PlayerCamera");

        if (camObj != null && camObj.TryGetComponent<Unity.Cinemachine.CinemachineCamera>(out var freeLookCam))
        {
            Transform target = lookPointTarget != null ? lookPointTarget : transform;

            freeLookCam.Follow = target;
            freeLookCam.LookAt = target;

            orbitalFollow = camObj.GetComponent<CinemachineOrbitalFollow>();
            ResetCameraBehindPlayer();

            Debug.Log($"[Camera Success] Kamera Player terkunci ke ID: {OwnerClientId}");
        }
        else
        {
            Debug.LogError("[Camera Error] Tidak menemukan objek bernama 'PlayerCamera' di Scene!");
        }
    }

    public void ResetCameraBehindPlayer()
    {
        if (orbitalFollow == null) return;

        if (OwnerClientId == 0)
        {
            orbitalFollow.HorizontalAxis.Value = 90f;
        }
        else if (OwnerClientId == 1)
        {
            orbitalFollow.HorizontalAxis.Value = 180f;
        }
        else if (OwnerClientId == 2)
        {
            orbitalFollow.HorizontalAxis.Value = 270f;
        }
        else if (OwnerClientId == 3)
        {
            orbitalFollow.HorizontalAxis.Value = 0f;
        }

        orbitalFollow.VerticalAxis.Value = orbitalFollow.VerticalAxis.Center;
    }
}