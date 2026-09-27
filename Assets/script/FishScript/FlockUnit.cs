using System.Collections.Generic;
using UnityEngine;

public class FlockUnit : MonoBehaviour
{
    [SerializeField] private float FOVAngle;
    [SerializeField] private float smoothDamp;

    private List<FlockUnit> cohesionNeighbours = new List<FlockUnit>();
    private List<FlockUnit> avoidanceNeighbours = new List<FlockUnit>();
    private List<FlockUnit> aligmentNeighbours = new List<FlockUnit>();
    private Flock assignFlock;
    private Vector3 currentVelocity;
    //{get set}을 하는 이유는 public이어도 값을 읽는것 하고 수정을 한느것을 재한을 하기 위해서
    public Transform myTransform { get; set; }
    private float speed;

    public void Awake()
    {
        //내 유닛(물고기2) 위치(자동)
        myTransform = transform;
    }

    public void InitializeSpeed(float speed)
    {
        this.speed = speed;
    }

    public void AssignFlock(Flock flock)
    {
        //flock(다른 스크립트) 가지고 옴
        assignFlock = flock;
    }

    public void MoveUnit()
    {
        FindNeighbours();
        CalculateSpeed();
        Vector3 cohesionVector = CalculateCohesionVector() * assignFlock.cohesionWeight;
        Vector3 avoidanceVector = CalculateAvoidacneVector() * assignFlock.avoidanceWeight;
        Vector3 aligementVector = CalculatAligementVector() * assignFlock.aligementWeight;
        Vector3 boundVector = CalculateBoundVector() * assignFlock.boundWeight;

        Vector3 moveVector = cohesionVector + avoidanceVector + aligementVector + boundVector;
        moveVector = Vector3.SmoothDamp(-myTransform.forward, moveVector, ref currentVelocity, smoothDamp);
        moveVector = moveVector.normalized * speed;
        //만약 현제 물고기 방향이 존재를 한다면(if문은 (0,0,0)을 방지)
        //주위에 아무것도 없으면(처음 시작을 할때) 0을 줌
        //에초에 방향이 (0,0,0)인것은 걍 말이 안된다.
        if (moveVector != Vector3.zero)
        { 
            // 머리가 진행 방향을 바라보게 회전
            myTransform.forward = -moveVector;

            // 실제로 머리가 향하는 쪽으로 전진
            myTransform.position += moveVector * Time.deltaTime;
        }
    }

    private void FindNeighbours()
    {
        cohesionNeighbours.Clear();
        avoidanceNeighbours.Clear();
        aligmentNeighbours.Clear();

        var allUnits = assignFlock.allUnits;
        for (int i = 0; i < allUnits.Length; i++)
        {
            var currentUnit = allUnits[i];
            //여기까지 100개정도 되는 flockUnits들 모임에서 하나하나 가지고 옴
            //FlockUnits라는 이 객채 자체를 가지고 오는거임.
            if (currentUnit != this)
            {
                //(물고기1 위치 - 나 자신의(물고기2) 위치)^2
                float currentNeighbourDistanceSqr = Vector3.SqrMagnitude(currentUnit.transform.position - myTransform.position);
                //물고기1 <-----> 나 자신 범위가 있음 그 범위를 벘어나면 일단 배재
                //아니면 FlockUnit 배열안에 삽입
                if(currentNeighbourDistanceSqr <= assignFlock.cohesionDistance * assignFlock.cohesionDistance)
                {
                    cohesionNeighbours.Add(currentUnit);
                }
                if (currentNeighbourDistanceSqr <= assignFlock.avoidanceDistance * assignFlock.avoidanceDistance)
                {
                    avoidanceNeighbours.Add(currentUnit);
                }
                if (currentNeighbourDistanceSqr <= assignFlock.aligementDistance * assignFlock.aligementDistance)
                {
                    aligmentNeighbours.Add(currentUnit);
                }
            }
        }
    }
    private void CalculateSpeed()
    {
        float speedSum = 0;
        if (cohesionNeighbours.Count == 0)
            return;

        for (int i = 0; i < cohesionNeighbours.Count; i++)
        {
            //일단은 전부다 speed들을 중첩을 한다
            speedSum += cohesionNeighbours[i].speed;
        }
        //그리고 전부다 나눠준다.(속도의 평균을 얻는거다)
        speed = speedSum/cohesionNeighbours.Count;
        //그리고 이 평균이 max와 min을 넘지 않도록 clamp를 해준다.
        speed = Mathf.Clamp(speed, assignFlock.minSpeed, assignFlock.maxSpeed);
    }
    private Vector3 CalculateCohesionVector()
    {
        Vector3 cohesionVector = Vector3.zero;
        int neighboursInFOV = 0;
        if (cohesionNeighbours.Count == 0)
        {
            return cohesionVector;
        }
        //등록이 된 FlockUnit객채들을 한번 돌아보삼
        for (int i = 0; i < cohesionNeighbours.Count; i++) {
            //각각 등록이 된 FlockUnit이 현제 물고기하고 각도가 너무 벌어진 상태는 아닌지 체크
            if (IsInFov(cohesionNeighbours[i].myTransform.position))
            {
                //각도가 그래도 정상이면 일단은 
                //현제 물고기 시야 안이므로 알단 이웃이 있다고 추가 +1
                neighboursInFOV++;
                //주변 등록된 물고기들의 위치 더 누적으로 더하기
                cohesionVector += cohesionNeighbours[i].myTransform.position;
            }
        }
        //현제 물고기2 시야에 물고기가 주변에 하나도 없으면 일단 현 위치를 반환
        if (neighboursInFOV == 0)
        {
            return cohesionVector;
        }
        //이웃 무리의 평균 무게중심(Center of Mass)
        cohesionVector /= neighboursInFOV;
        //현제 물고기2 위치에서 무리의 중심점을 향해 뻗어나가는 방향 벡터
        cohesionVector -= myTransform.position;
        //정규화
        cohesionVector = Vector3.Normalize(cohesionVector);
        //시야 안에 보이는 주변 동료들의 위치를 이용을 해서 무리의 가운데를 구한다.
        return cohesionVector;

    }
    private Vector3 CalculatAligementVector()
    {
        Vector3 avoidanceVector = myTransform.forward;
        if (avoidanceNeighbours.Count == 0)
            return avoidanceVector;
        int neighboursInFov = 0;
        for (int i = 0; i < avoidanceNeighbours.Count; i++)
        {
            if (IsInFov(avoidanceNeighbours[i].myTransform.forward))
            {
                neighboursInFov++;
                avoidanceVector += (myTransform.position - avoidanceNeighbours[i].myTransform.position);
            }
        }
        avoidanceVector /= neighboursInFov;
        avoidanceVector = avoidanceVector.normalized;
        return avoidanceVector;
    }

    private Vector3 CalculateAvoidacneVector()
    {
        Vector3 aligementVector = myTransform.forward;
        if(aligmentNeighbours.Count == 0)
            return aligementVector;
        int neighboursInFov = 0;
        for(int i = 0; i < aligmentNeighbours.Count; i++)
        {
            if (IsInFov(aligmentNeighbours[i].myTransform.forward))
            {
                neighboursInFov++;
                aligementVector += aligmentNeighbours[i].myTransform.forward;
            }
        }
        if (neighboursInFov == 0)
            return myTransform.forward; 
        aligementVector /= neighboursInFov;
        aligementVector = aligementVector.normalized;
        return aligementVector;
    }
    private Vector3 CalculateBoundVector()
    {
        Vector3 offsetToCenter = assignFlock.transform.position - myTransform.position;
        bool isNearCenter = offsetToCenter.magnitude >= assignFlock.boundDistance * 0.9;
        return isNearCenter ? offsetToCenter.normalized : Vector3.zero;
    }

    private bool IsInFov(Vector3 p) {
        //물고기2 가 바라보는 방향, 내 유닛에서 상대방 유닛을 바라보는 방향성.
        //각도가 너무 크지 않으면 리턴 true
        return Vector3.Angle(-myTransform.forward, p - myTransform.position) <= FOVAngle;
    }
}
