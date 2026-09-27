using System;
using UnityEngine;

public class Flock : MonoBehaviour
{
    [Header("Spawn Setup")]
    //실제 (FlockUnit)이라는 스크립트
    [SerializeField] private FlockUnit flockUnitPrefab;
    //오브젝트 개수
    [SerializeField] private int flockSize;
    //범위
    [SerializeField] private Vector3 spawnBounds;

    [Header("Speed Setup")]
    [Range(0, 10)]
    [SerializeField] private float _maxSpeed;
    public float maxSpeed { get { return _maxSpeed; } }
    [Range(0, 10)]

    [SerializeField] private float _minSpeed;
    public float minSpeed { get { return _minSpeed; } }

    //걍 유니티 에디터에서 깔금하게 보여주기 위함이다
    [Header("Detection Distances")]
    //현제 물고기 <---> 이웃 물고기 최대 거리
    //_cohesionDistance를 우리가 조절을 할 수 있음.
    [Range(0, 10)]
    [SerializeField] private float _cohesionDistance;
    public float cohesionDistance { get { return _cohesionDistance; } }
    [Range(0, 10)]
    [SerializeField] private float _avoidanceDistance;
    public float avoidanceDistance { get { return _avoidanceDistance; } }
    [Range(0, 10)]
    [SerializeField] private float _aligementDistance;
    public float aligementDistance { get { return _aligementDistance; } }
    [Range(0, 100)]
    [SerializeField] private float _boundDistance;
    public float boundDistance { get { return _boundDistance; } }

    [Header("Behavior Weight")]
    //현제 물고기 <---> 이웃 물고기 최대 거리
    //_cohesionDistance를 우리가 조절을 할 수 있음.
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
    [SerializeField] private float _boundWeight;
    public float boundWeight { get { return _boundWeight; } }
    //스크립트 모임
    public FlockUnit[] allUnits { get; set; }

    private void Start()
    {
        GenerateUnits();
    }

    private void Update()
    {
        //모든 물고기들 움직이기
        for (int i = 0; i < allUnits.Length; i++)
        {
            allUnits[i].MoveUnit();
        }
    }
    private void GenerateUnits()
    {
        //array만들기 딱 물고기 개수 만큼만
        allUnits = new FlockUnit[flockSize];
        for (int i = 0; i < flockSize; i++)
        {
            //1크기 반지름이 최대인 숫자를 준다(0.0 ~ 0.99)
            var randomVector = UnityEngine.Random.insideUnitSphere;
            //10(범위 최대 크기) * 0.45 랜덤 숫자(1 아레) 
            randomVector = new Vector3(randomVector.x * spawnBounds.x, randomVector.y * spawnBounds.y, randomVector.z * spawnBounds.z);
            //스폰 위치 적용
            var spawnPosition = transform.position + randomVector;
            //Y축만 돌리는 거임 나머지는 고정
            var rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            //메모리에다가 등록(객채화)-->(prefab(skin), spawn, roation)등을 받아서 새로운 객채를 만든다
            //하지만 여기서는 일단은 FlockUnit이라는 스크립트를 집어넣음.
            allUnits[i] = Instantiate(flockUnitPrefab, spawnPosition, rotation);
            //자기 자신을 AssignFlock라는 메서드에다가 보내주기
            allUnits[i].AssignFlock(this);
            allUnits[i].InitializeSpeed(UnityEngine.Random.Range(minSpeed, maxSpeed));
        }
    }
}
