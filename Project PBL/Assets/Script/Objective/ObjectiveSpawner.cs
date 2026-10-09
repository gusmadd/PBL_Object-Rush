using Unity.Netcode;
using UnityEngine;

public class ObjectiveSpawner : NetworkBehaviour
{
    public static ObjectiveSpawner Instance { get; private set; }

    [Header("Objective Prefabs (4 Variasi)")]
    [SerializeField] private GameObject[] objectivePrefabs;

    [Header("Circle Spawn Area Settings")]
    [Tooltip("Pusat area spawn (posisi tengah)")]
    [SerializeField] private Vector3 centerPoint = new Vector3(0f, 1.5f, 0f);
    public Vector3 CenterPoint => centerPoint;

    [Tooltip("Jari-jari/Radius area lingkaran spawn")]
    [SerializeField] private float spawnRadius = 5f;

    private NetworkObject currentObjectiveNetObj;
    private bool isSpawning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        //kosong
    }

    // --- FUNGSI AMBIL POSISI ACAK DI DALAM LINGKARAN ---
    private Vector3 GetRandomCirclePosition()
    {
        // Random.insideUnitCircle menghasilkan koordinat acak (X, Y) dalam radius 1
        Vector2 randomCirclePoint = Random.insideUnitCircle * spawnRadius;

        // Petakan ke X dan Z (karena lantai/ground ada di bidang XZ)
        Vector3 randomPosition = new Vector3(
            centerPoint.x + randomCirclePoint.x,
            centerPoint.y,
            centerPoint.z + randomCirclePoint.y
        );

        return randomPosition;
    }

    public void SpawnRandomObjective()
    {
        if (!IsServer || isSpawning) return;

        if (objectivePrefabs == null || objectivePrefabs.Length == 0)
        {
            Debug.LogError("[ObjectiveSpawner] Array objectivePrefabs masih kosong!");
            return;
        }

        isSpawning = true;

        // Dapatkan posisi acak di dalam lingkaran
        Vector3 randomSpawnPosition = GetRandomCirclePosition();

        // Pilih prefab acak
        int randomIndex = Random.Range(0, objectivePrefabs.Length);
        GameObject selectedPrefab = objectivePrefabs[randomIndex];

        // Instantiate & Spawn lewat Netcode
        GameObject newObjective = Instantiate(selectedPrefab, randomSpawnPosition, Quaternion.identity);
        currentObjectiveNetObj = newObjective.GetComponent<NetworkObject>();

        if (currentObjectiveNetObj != null)
        {
            currentObjectiveNetObj.Spawn();
            Debug.Log($"<color=cyan>[ObjectiveSpawner] Spawn {selectedPrefab.name} di area lingkaran: {randomSpawnPosition}</color>");
        }

        isSpawning = false;
    }

    public void DespawnAndRespawnNext(NetworkObject objectiveToDespawn)
    {
        if (!IsServer || isSpawning) return;

        if (objectiveToDespawn != null && objectiveToDespawn.IsSpawned)
        {
            isSpawning = true;

            objectiveToDespawn.Despawn(true);
            currentObjectiveNetObj = null;

            isSpawning = false;
            SpawnRandomObjective();
        }
    }

    // --- VISUAL GIZMOS LINGKARAN DI UNITY EDITOR ---
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        // Gambar garis lingkaran di Scene View
        int segments = 30;
        float angle = 0f;
        Vector3 lastPoint = centerPoint + new Vector3(Mathf.Cos(0) * spawnRadius, 0f, Mathf.Sin(0) * spawnRadius);

        for (int i = 1; i <= segments; i++)
        {
            angle += (2f * Mathf.PI) / segments;
            Vector3 nextPoint = centerPoint + new Vector3(Mathf.Cos(angle) * spawnRadius, 0f, Mathf.Sin(angle) * spawnRadius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}