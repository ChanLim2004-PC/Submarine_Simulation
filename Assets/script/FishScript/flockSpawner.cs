using UnityEngine;

public class FlockSpawner : MonoBehaviour
{
    public GameObject flockUnitPrefab;
    public int spawnCount = 5;

    [Header("Spawn Around Camera")]
    [SerializeField] private float spawnDistanceMin = 10f;   // 재배치될 때 최소 거리
    [SerializeField] private float spawnDistanceMax = 100f;  // 재배치될 때 최대 거리
    [SerializeField] private float spawnSpreadRadius = 30f;  // 퍼지는 반경

    void Start()
    {
        SpawnFlockAroundCamera();
    }

    void SpawnFlockAroundCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform camTransform = cam.transform;

        for (int i = 0; i < spawnCount; i++)
        {
            //카메라 정면 방향을 기준으로 일정 거리 떨어진 지점 계산
            float randomDistance = Random.Range(spawnDistanceMin, spawnDistanceMax);
            Vector3 baseSpawnPos = camTransform.position + (camTransform.forward * randomDistance);

            //정면 위치 주변으로 원형/구형 무작위 오프셋주기
            Vector3 randomOffset = Random.insideUnitSphere * spawnSpreadRadius;
            Vector3 finalSpawnPos = baseSpawnPos + randomOffset;

            //유닛 생성
            Instantiate(flockUnitPrefab, finalSpawnPos, camTransform.rotation);
        }
    }
}