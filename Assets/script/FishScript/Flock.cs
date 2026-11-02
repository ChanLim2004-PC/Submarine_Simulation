using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;

public class Flock : MonoBehaviour
{
    [Header("Spawn Setup")]
    [SerializeField] private FlockUnit[] flockUnits;
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

    [Header("Ocean Layer Active Range (이 매니저가 켜질 수심 구간)")]
    [SerializeField] private float minActiveDepth = 0f;    
    [SerializeField] private float maxActiveDepth = 0f; 

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
    private void DepthSpawn(Camera cam)
    {
	if (cam == null || allUnits == null) return;

	float currentDepth = 0 - cam.transform.position.y;
	currentDepth = Mathf.Max(0f, currentDepth);

	float densityRatio = 0f;
	float fadeMargin = 30f; 

	if (currentDepth >= minActiveDepth && currentDepth < maxActiveDepth)
	{
	    float fadeIn = Mathf.InverseLerp(minActiveDepth, minActiveDepth + fadeMargin, currentDepth);

	    float fadeOut = Mathf.InverseLerp(maxActiveDepth, maxActiveDepth - fadeMargin, currentDepth);

	    densityRatio = Mathf.Min(fadeIn, fadeOut);
	}

	int activeCount = Mathf.RoundToInt(allUnits.Length * densityRatio);

	for (int i = 0; i < allUnits.Length; i++)
	{
	    if (allUnits[i] != null)
	    {
		bool shouldBeActive = i < activeCount;
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

        timer += Time.deltaTime;
        if (timer >= moveInterval || targetWanderPosition == Vector3.zero)
        {
            timer = 0f;
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * wanderRadius;

            targetWanderPosition = cam.transform.position + (cam.transform.forward * 10f) + randomOffset;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetWanderPosition, moveSpeed * Time.deltaTime);
    }
    private void recycleFlock(Camera cam)
    {
        if (cam != null)
        {
            Transform camTransform = cam.transform;

            for (int i = 0; i < allUnits.Length; i++)
            {
                if (allUnits[i] == null) continue;

                float distanceToCam = Vector3.Distance(allUnits[i].myTransform.position, camTransform.position);

                if (distanceToCam > maxFollowDistance || distanceToCam < 5f)
                {
                    float randomDistance = UnityEngine.Random.Range(spawnDistanceMin, spawnDistanceMax);
                    Vector3 basePos = camTransform.position + (camTransform.forward * randomDistance);
                    Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnSpreadRadius;
                    Vector3 newSpawnPos = basePos + randomOffset;

                    allUnits[i].myTransform.position = newSpawnPos;

                    allUnits[i].currentVelocity = Vector3.zero;
                    allUnits[i].myTransform.rotation = camTransform.rotation;
                }
            }
        }
    }
    private void Update()
    {
        Camera cam = Camera.main;
        DepthSpawn(cam);
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
            fovAngle = 270f,
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            smoothDamp = 0.1f,
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
        if (flockUnits == null || flockUnits.Length == 0) return;

        Camera cam = Camera.main;
        Vector3 spawnCenter = cam != null ? cam.transform.position : transform.position;

        allUnits = new FlockUnit[flockSize];
        for (int i = 0; i < flockSize; i++)
        {
            // 이 매니저에 등록된 프리팹들 중 하나를 골라서 생성
            FlockUnit selectedPrefab = flockUnits[UnityEngine.Random.Range(0, flockUnits.Length)];
            var pos = spawnCenter + UnityEngine.Random.insideUnitSphere * 20f;
            
            var unit = Instantiate(selectedPrefab, pos, Quaternion.identity);
            unit.AssignFlock(this);
            unit.InitializeSpeed(UnityEngine.Random.Range(minSpeed, maxSpeed));
            allUnits[i] = unit;
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
