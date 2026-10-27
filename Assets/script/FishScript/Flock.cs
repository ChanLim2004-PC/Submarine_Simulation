using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;

public class Flock : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private FlockUnit flockUnitPrefab;
    [SerializeField] private int flockSize;
    [SerializeField] private Vector3 spawnBounds;

    [Header("Speed Setup")]
    [Range(0, 10)]
    [SerializeField] private float _minSpeed;
    public float minSpeed { get { return _minSpeed; } }
    [Range(0, 10)]
    [SerializeField] private float _maxSpeed;
    public float maxSpeed { get { return _maxSpeed; } }

    [Header("Detection Distances")]

    [Range(0, 10)]
    [SerializeField] private float _cohesionDistance;
    public float cohesionDistance { get { return _cohesionDistance; } }

    [Range(0, 10)]
    [SerializeField] private float _avoidanceDistance;
    public float avoidanceDistance { get { return _avoidanceDistance; } }

    [Range(0, 10)]
    [SerializeField] private float _aligementDistance;
    public float aligementDistance { get { return _aligementDistance; } }

    [Range(0, 10)]
    [SerializeField] private float _obstacleDistance;
    public float obstacleDistance { get { return _obstacleDistance; } }

    [Range(0, 100)]
    [SerializeField] private float _boundsDistance;
    public float boundsDistance { get { return _boundsDistance; } }


    [Header("Behaviour Weights")]

    [Range(0, 10)]
    [SerializeField] private float _cohesionWeight;
    public float cohesionWeight { get { return _cohesionWeight; } }

    [Range(0, 10)]
    [SerializeField] private float _avoidanceWeight;
    public float avoidanceWeight { get { return _avoidanceWeight; } }

    [Range(0, 10)]
    [SerializeField] private float _aligementWeight;
    public float aligementWeight { get { return _aligementWeight; } }

    [Range(0, 10)]
    [SerializeField] private float _boundsWeight;
    public float boundsWeight { get { return _boundsWeight; } }

    [Range(0, 100)]
    [SerializeField] private float _obstacleWeight;

    [Header("Camera & Move Setup")]
    [SerializeField] private float moveInterval = 3f;      // 위치를 랜덤하게 바꿀 주기 (초)
    [SerializeField] private float wanderRadius = 15f;     // 카메라 주변에서 움직일 최대 반경
    [SerializeField] private float moveSpeed = 5f;         // 부드럽게 이동하는 속도

    [Header("Camera Culling & Recycling Setup")]
    [SerializeField] private float maxFollowDistance = 10f; // 카메라로부터 이 거리 이상 멀어지면 재배치
    [SerializeField] private float spawnDistanceMin = 10f;   // 재배치될 때 최소 거리
    [SerializeField] private float spawnDistanceMax = 10f;  // 재배치될 때 최대 거리
    [SerializeField] private float spawnSpreadRadius = 30f;  // 퍼지는 반경

    [Header("Ocean Layer Depth Settings")]
    [SerializeField] private float surfaceY = 0f;          // 수면 Y 좌표
    [SerializeField] private float photicLimit = 200f;      // 유광층 끝 (예: -200m)
    [SerializeField] private float boundaryLimit = 500f;    // 경계층 끝 (예: -500m)
    [SerializeField] private float backlitLimit = 1500f;    // 역광측 끝 (예: -1500m)
    // 그 아래는 전부 무광층(Aphotic)

    private Vector3 targetWanderPosition;
    private float timer;
    public float obstacleWeight { get { return _obstacleWeight; } }

    public FlockUnit[] allUnits { get; set; }

    private void Start()
    {
        GenerateUnits();
        //SetRandomFlockValues();
    }

    private void SetRandomFlockValues()
    {
        _boundsDistance = UnityEngine.Random.Range(5f, 30f);
        _cohesionWeight = UnityEngine.Random.Range(1f, 10f);
        _cohesionWeight = UnityEngine.Random.Range(1f, 10f);
        _avoidanceWeight = UnityEngine.Random.Range(1f, 10f);
        _minSpeed = UnityEngine.Random.Range(1f, 2f);
        _maxSpeed = UnityEngine.Random.Range(2f, 5f);
    }
    
    private void depthSpawn(Camera cam)
    {
        if (cam != null)
        {
            float currentDepth = surfaceY - cam.transform.position.y;
            currentDepth = Mathf.Max(0f, currentDepth);

            float targetDensityRatio = 1f;

            // 2. 수심에 따른 구간(Layer) 판별 및 밀도 결정
            if (currentDepth <= photicLimit)
            {
                
                targetDensityRatio = 1.0f; // 100% 다 킴
            }
            else if (currentDepth <= boundaryLimit)
            {
                
                // 유광층에서 경계층으로 갈수록 서서히 줄어듦 (1.0 -> 0.5)
                targetDensityRatio = Mathf.InverseLerp(boundaryLimit, photicLimit, currentDepth) * 0.5f + 0.5f;
            }
            else if (currentDepth <= backlitLimit)
            {
              
                // 경계층에서 역광측으로 갈수록 더 줄어듦 (0.5 -> 0.1)
                targetDensityRatio = Mathf.InverseLerp(backlitLimit, boundaryLimit, currentDepth) * 0.4f + 0.1f;
            }
            else
            {
               
                targetDensityRatio = 0.0f; // 심해 무광층은 물고기 끄기!
            }

            // 3. 계산된 비율에 맞춰 유닛 켜기/끄기 갱신
            int activeUnitCount = Mathf.RoundToInt(allUnits.Length * targetDensityRatio);
            for (int i = 0; i < allUnits.Length; i++)
            {
                if (allUnits[i] == null) continue;

                bool shouldBeActive = i < activeUnitCount;
                if (allUnits[i].gameObject.activeSelf != shouldBeActive)
                {
                    allUnits[i].gameObject.SetActive(shouldBeActive);
                }
            }
        }
    }

    private void moveFlock(Camera cam)
    {
        if (cam == null) return;

        // 타이머를 이용해 일정 시간마다 카메라 주변의 새로운 랜덤 목표 지점(RandomVector)을 갱신
        timer += Time.deltaTime;
        if (timer >= moveInterval || targetWanderPosition == Vector3.zero)
        {
            timer = 0f;
            // 카메라 위치 기준, 구(Sphere) 안쪽의 랜덤한 오프셋 생성
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * wanderRadius;

            // 카메라 정면 앞쪽으로 위치가 잡히도록 기본 오프셋을 더해줄 수도 있음
            targetWanderPosition = cam.transform.position + (cam.transform.forward * 10f) + randomOffset;
        }

        // Flock 매니저 오브젝트를 목표 지점으로 부드럽게 이동시킴 (Lerp 또는 MoveTowards)
        transform.position = Vector3.MoveTowards(transform.position, targetWanderPosition, moveSpeed * Time.deltaTime);
    }
    private void recycleFlock(Camera cam)
    {
        if (cam != null)
        {
            Transform camTransform = cam.transform;

            // 모든 유닛을 돌면서 카메라와의 거리를 체크
            for (int i = 0; i < allUnits.Length; i++)
            {
                if (allUnits[i] == null) continue;

                // 유닛과 카메라 사이의 거리 계산
                float distanceToCam = Vector3.Distance(allUnits[i].myTransform.position, camTransform.position);

                // 만약 유닛이 카메라 뒤로 너무 멀어졌거나 범위를 벗어났다면?
                if (distanceToCam > maxFollowDistance || distanceToCam < 5f)
                {
                    // 카메라 정면 앞쪽의 무작위 새로운 위치 계산
                    float randomDistance = UnityEngine.Random.Range(spawnDistanceMin, spawnDistanceMax);
                    Vector3 basePos = camTransform.position + (camTransform.forward * randomDistance);
                    Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnSpreadRadius;
                    Vector3 newSpawnPos = basePos + randomOffset;

                    // 위치 강제 재배치 (Teleport)
                    allUnits[i].myTransform.position = newSpawnPos;

                    // 유닛의 속도와 방향도 카메라 방향으로 초기화해주면 부드러움
                    allUnits[i].currentVelocity = Vector3.zero;
                    allUnits[i].myTransform.rotation = camTransform.rotation;
                }
            }
        }
    }
    private void Update()
    {
        Camera cam = Camera.main;
        depthSpawn(cam);
        recycleFlock(cam);
        moveFlock(cam);

        NativeArray<Vector3> unitForwardDirections = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> unitCurrentVelocities = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> unitPositions = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> cohesionNeighbours = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> avoidanceNeighbours = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> aligementNeighbours = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<Vector3> aligementNeighboursDirecions = new NativeArray<Vector3>(allUnits.Length, Allocator.TempJob);
        NativeArray<float> allUnitsSpeeds = new NativeArray<float>(allUnits.Length, Allocator.TempJob);
        NativeArray<float> neighbourSpeeds = new NativeArray<float>(allUnits.Length, Allocator.TempJob);

        for (int i = 0; i < allUnits.Length; i++)
        {
            unitForwardDirections[i] = allUnits[i].myTransform.forward;
            unitCurrentVelocities[i] = allUnits[i].currentVelocity;
            unitPositions[i] = allUnits[i].myTransform.position;
            cohesionNeighbours[i] = Vector3.zero;
            avoidanceNeighbours[i] = Vector3.zero;
            aligementNeighbours[i] = Vector3.zero;
            aligementNeighboursDirecions[i] = Vector3.zero;
            allUnitsSpeeds[i] = allUnits[i].speed;
            neighbourSpeeds[i] = 0f;
        }

        MoveJob moveJob = new MoveJob
        {
            unitForwardDirections = unitForwardDirections,
            unitCurrentVelocities = unitCurrentVelocities,
            unitPositions = unitPositions,
            cohesionNeighbours = cohesionNeighbours,
            avoidanceNeighbours = avoidanceNeighbours,
            aligementNeighbours = aligementNeighbours,
            aligementNeighboursDirecions = aligementNeighboursDirecions,
            allUnitsSpeeds = allUnitsSpeeds,
            neighbourSpeeds = neighbourSpeeds,
            cohesionDistance = cohesionDistance,
            avoidanceDistance = avoidanceDistance,
            aligementDistance = aligementDistance,
            boundsDistance = boundsDistance,
            obstacleDistance = obstacleDistance,
            cohesionWeight = cohesionWeight,
            avoidanceWeight = avoidanceWeight,
            aligementWeight = aligementWeight,
            boundsWeight = boundsWeight,
            obstacleWeight = obstacleWeight,
            fovAngle = flockUnitPrefab.FOVAngle,
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            smoothDamp = flockUnitPrefab.smoothDamp,
            flockPosition = transform.position,
            deltaTime = Time.deltaTime
        };

        JobHandle handle = moveJob.Schedule(allUnits.Length, 5);
        handle.Complete();
        for (int i = 0; i < allUnits.Length; i++)
        {
            allUnits[i].myTransform.forward = unitForwardDirections[i];
            allUnits[i].myTransform.position = unitPositions[i];
            allUnits[i].currentVelocity = unitCurrentVelocities[i];
            allUnits[i].speed = allUnitsSpeeds[i];
        }

        unitForwardDirections.Dispose();
        unitCurrentVelocities.Dispose();
        unitPositions.Dispose();
        cohesionNeighbours.Dispose();
        avoidanceNeighbours.Dispose();
        aligementNeighbours.Dispose();
        aligementNeighboursDirecions.Dispose();
        allUnitsSpeeds.Dispose();
        neighbourSpeeds.Dispose();

    }

    private void GenerateUnits()
    {
        allUnits = new FlockUnit[flockSize];
        for (int i = 0; i < flockSize; i++)
        {
            var randomVector = UnityEngine.Random.insideUnitSphere;
            randomVector = new Vector3(randomVector.x * spawnBounds.x, randomVector.y * spawnBounds.y, randomVector.z * spawnBounds.z);
            var spawnPosition = transform.position + randomVector;
            var rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            allUnits[i] = Instantiate(flockUnitPrefab, spawnPosition, rotation);
            allUnits[i].AssignFlock(this);
            allUnits[i].InitializeSpeed(UnityEngine.Random.Range(minSpeed, maxSpeed));
        }
    }
}

[BurstCompile]
public struct MoveJob : IJobParallelFor
{
    public NativeArray<Vector3> unitCurrentVelocities;

    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> unitForwardDirections;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> unitPositions;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> cohesionNeighbours;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> avoidanceNeighbours;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> aligementNeighbours;
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> aligementNeighboursDirecions;
    [NativeDisableParallelForRestriction]
    public NativeArray<float> allUnitsSpeeds;
    [NativeDisableParallelForRestriction]
    public NativeArray<float> neighbourSpeeds;

    public Vector3 flockPosition;
    public float cohesionDistance;
    public float avoidanceDistance;
    public float aligementDistance;
    public float boundsDistance;
    public float obstacleDistance;
    public float cohesionWeight;
    public float avoidanceWeight;
    public float aligementWeight;
    public float boundsWeight;
    public float obstacleWeight;
    public float fovAngle;
    public float minSpeed;
    public float maxSpeed;
    public float smoothDamp;
    public float deltaTime;


    public void Execute(int index)
    {
        //Find Neighbours
        int cohesionIndex = 0;
        int avoidanceIndex = 0;
        int aligementIndex = 0;
        for (int i = 0; i < unitPositions.Length; i++)
        {
            Vector3 currentUnitPosition = unitPositions[index];
            Vector3 currentNeighbourPosition = unitPositions[i];
            Vector3 currentNeighbourDirection = unitForwardDirections[i];
            if (currentUnitPosition != currentNeighbourPosition)
            {
                float currentDistanceToNeighbourSqr = Vector3.SqrMagnitude(currentUnitPosition - currentNeighbourPosition);
                if (currentDistanceToNeighbourSqr < cohesionDistance * cohesionDistance)
                {
                    cohesionNeighbours[cohesionIndex] = currentNeighbourPosition;
                    neighbourSpeeds[cohesionIndex] = allUnitsSpeeds[i];
                    cohesionIndex++;
                }
                if (currentDistanceToNeighbourSqr < avoidanceDistance * avoidanceDistance)
                {
                    avoidanceNeighbours[avoidanceIndex] = currentNeighbourPosition;
                    avoidanceIndex++;
                }
                if (currentDistanceToNeighbourSqr < aligementDistance * aligementDistance)
                {
                    aligementNeighbours[aligementIndex] = currentNeighbourPosition;
                    aligementNeighboursDirecions[aligementIndex] = currentNeighbourDirection;
                    aligementIndex++;
                }
            }
        }

        //Calculate speed
        float speed = 0f;
        if (cohesionNeighbours.Length != 0)
        {
            for (int i = 0; i < cohesionNeighbours.Length; i++)
            {
                speed += neighbourSpeeds[i];
            }
            speed /= cohesionNeighbours.Length;

        }
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        //Calculate cohesion
        Vector3 cohesionVector = Vector3.zero;
        if (cohesionNeighbours.Length != 0)
        {
            int cohesionNeighbourdInFOV = 0;
            for (int i = 0; i <= cohesionIndex; i++)
            {
                if (IsInFov(unitForwardDirections[index], unitPositions[index], cohesionNeighbours[i], fovAngle) && cohesionNeighbours[i] != Vector3.zero)
                {
                    cohesionNeighbourdInFOV++;
                    cohesionVector += cohesionNeighbours[i];
                }
            }
            cohesionVector /= cohesionNeighbourdInFOV;
            cohesionVector -= unitPositions[index];
            cohesionVector = cohesionVector.normalized * cohesionWeight;
        }

        //Calculate avoidance
        Vector3 avoidanceVector = Vector3.zero;
        if (avoidanceNeighbours.Length != 0)
        {
            int avoidanceNeighbourdInFOV = 0;
            for (int i = 0; i <= avoidanceIndex; i++)
            {
                if (IsInFov(unitForwardDirections[index], unitPositions[index], avoidanceNeighbours[i], fovAngle) && avoidanceNeighbours[i] != Vector3.zero)
                {
                    avoidanceNeighbourdInFOV++;
                    avoidanceVector += (unitPositions[index] - avoidanceNeighbours[i]);
                }
            }

            avoidanceVector /= avoidanceNeighbourdInFOV;
            avoidanceVector = avoidanceVector.normalized * avoidanceWeight;
        }

        //Calculate aligement
        Vector3 aligementVector = Vector3.zero;
        if (aligementNeighbours.Length != 0)
        {
            int aligementNeighbourdInFOV = 0;
            for (int i = 0; i <= aligementIndex; i++)
            {
                if (IsInFov(unitForwardDirections[index], unitPositions[index], aligementNeighbours[i], fovAngle) && aligementNeighbours[i] != Vector3.zero)
                {
                    aligementNeighbourdInFOV++;
                    aligementVector += aligementNeighboursDirecions[i].normalized;
                }
            }
            aligementVector /= aligementNeighbourdInFOV;
            aligementVector = aligementVector.normalized * aligementWeight;
        }

        //Calculate bounds
        Vector3 offsetToCenter = flockPosition - unitPositions[index];
        bool isNearBound = offsetToCenter.magnitude >= boundsDistance * 0.9f;
        Vector3 boundsVector = isNearBound ? offsetToCenter.normalized : Vector3.zero;
        boundsVector *= boundsWeight;

        //Move Unit
        Vector3 currentVelocity = unitCurrentVelocities[index];
        Vector3 moveVector = cohesionVector + avoidanceVector + aligementVector + boundsVector;

        moveVector = Vector3.SmoothDamp(-unitForwardDirections[index], moveVector, ref currentVelocity, smoothDamp, 10000, deltaTime);

        moveVector = moveVector.normalized * speed;
        if (moveVector == Vector3.zero)
        {
            moveVector = -unitForwardDirections[index];
        }
        unitPositions[index] = unitPositions[index] + moveVector * deltaTime;
        unitForwardDirections[index] = -moveVector.normalized;
        allUnitsSpeeds[index] = speed;
        unitCurrentVelocities[index] = currentVelocity;


    }
    private bool IsInFov(Vector3 forward, Vector3 unitPosition, Vector3 targetPosition, float angle)
    {
        return Vector3.Angle(forward, targetPosition - unitPosition) <= angle;
    }

}