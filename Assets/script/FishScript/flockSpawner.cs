using UnityEngine;

public class FlockSpawner : MonoBehaviour
{
    [Header("Spawn Prefabs")]
    [SerializeField] private GameObject[] flockPrefabs; 

    public int spawnCount = 5;

    [Header("Spawn Around Camera")]
    [SerializeField] private float spawnDistanceMin = 10f;   
    [SerializeField] private float spawnDistanceMax = 100f;  
    [SerializeField] private float spawnSpreadRadius = 30f;  

    void Start()
    {
        SpawnFlockAroundCamera();
    }
    void SpawnFlockAroundCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (flockPrefabs == null || flockPrefabs.Length == 0)
        {
            return;
        }

        Transform camTransform = cam.transform;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject selectedPrefab = flockPrefabs[Random.Range(0, flockPrefabs.Length)];

            float randomDistance = Random.Range(spawnDistanceMin, spawnDistanceMax);
            Vector3 baseSpawnPos = camTransform.position + (camTransform.forward * randomDistance);

            Vector3 randomOffset = Random.insideUnitSphere * spawnSpreadRadius;
            Vector3 finalSpawnPos = baseSpawnPos + randomOffset;

            Instantiate(selectedPrefab, finalSpawnPos, camTransform.rotation);
        }
    }
}
