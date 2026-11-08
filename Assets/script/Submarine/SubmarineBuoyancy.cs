using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SubmarineController))]
public class SubmarineBuoyancy : MonoBehaviour
{
    #region 인스펙터 설정

    [Header("잠수함 제원")]
    // 잠수함이 밀어내는 물의 부피 (m³). 부력 = 밀도 × 부피 × g
    public float displacedVolume = 40f;

    // 탱크가 빈 상태의 잠수함 질량 (kg)
    public float hullDryMass = 8000f;

    // 선체 높이 (m). 수면을 뚫고 올라올 때 잠긴 비율 계산에 사용--------------폐기 예정
    public float hullHeight = 4f;

    [Header("주부력 탱크")]
    // 탱크 최대 용량 (m³)
    public float ballastCapacity = 8f;

    // Ctrl/Space를 누를 때 초당 변하는 탱크 수위 (0~1 기준)
    public float ballastFlowRate = 0.15f;

    // Z 키로 중성부력까지 맞추는 데 걸리는 시간 (s)
    public float neutralizeTime = 2f;

    [Header("상태 (확인용)")]
    // 탱크 수위 (0 = 비어 있음/부력 최대, 1 = 가득 참/부력 최소)
    [Range(0f, 1f)]
    public float ballastFill = 0.5f;

    // 중성부력 자동 조절 중인지 여부
    public bool isNeutralizing = false;

    // 순부력 (N). 양수면 상승, 음수면 하강
    public float netForce;

    [Header("수직 항력")]
    // 수직 선형 항력 (1/s). 클수록 천천히 가속, 낮은 속도에서 빨리 멈춤
    public float verticalLinearDrag = 0.3f;

    // 수직 2차 항력 (1/m). 종단 속도를 결정
    public float verticalQuadraticDrag = 0.15f;

    #endregion

    #region 내부 변수

    private Rigidbody rb;
    private SubmarineController sub;

    private float ballastInput;   // +1: 물 채움(Ctrl), -1: 물 배출(Space)

    #endregion

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sub = GetComponent<SubmarineController>();
    }

    void Start()
    {
        // 시작할 때는 중성부력 상태로
        ballastFill = GetNeutralFill();
    }

    // 입력은 Update에서 읽음
    void Update()
    {
        ballastInput = 0f;

        if (Keyboard.current == null)
            return;

        // Ctrl: 탱크에 바닷물을 채움 -> 부력 감소
        if (Keyboard.current.ctrlKey.isPressed)
            ballastInput += 1f;

        // Space: 탱크의 물을 배출 -> 부력 증가
        if (Keyboard.current.spaceKey.isPressed)
            ballastInput -= 1f;

        // Z: 중성부력으로 복귀 시작
        if (Keyboard.current.zKey.wasPressedThisFrame)
            isNeutralizing = true;

        // 수동 조작을 하면 자동 중성화 해제
        if (!Mathf.Approximately(ballastInput, 0f))
            isNeutralizing = false;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        UpdateBallast(dt);
        ApplyBuoyancy(dt);
    }

    // 탱크 수위 갱신
    private void UpdateBallast(float dt)
    {
        if (isNeutralizing)
        {
            // 짧은 시간에 걸쳐 중성부력 수위로 이동
            float target = GetNeutralFill();
            ballastFill = Mathf.MoveTowards(
                ballastFill, target, dt / neutralizeTime);
        }
        else
        {
            ballastFill += ballastInput * ballastFlowRate * dt;
        }

        ballastFill = Mathf.Clamp01(ballastFill);
    }

    // 부력과 중력의 합력을 수직 가속도로 적용
    // 부력과 중력의 합력으로 수직 속도만 갱신 (수평 속도는 건드리지 않음)
    private void ApplyBuoyancy(float dt)
    {
        float g = Physics.gravity.magnitude;
        float density = sub.GetWaterDensity(sub.depth);

        // 탱크에 찬 물의 질량까지 포함한 전체 질량
        float totalMass = hullDryMass + density * ballastCapacity * ballastFill;

        // 부력: F = ρ V g (수면 위로 나온 만큼 감소)
        float buoyancy = density * displacedVolume * GetSubmergedFraction() * g;
        float weight = totalMass * g;

        netForce = buoyancy - weight;

        Vector3 v = rb.linearVelocity;

        // 가속
        float vy = v.y + (netForce / totalMass) * dt;

        // 수직 항력 (암시적 적분: 반전/발산 없음)
        float dragRate = verticalLinearDrag + verticalQuadraticDrag * Mathf.Abs(vy);
        vy /= (1f + dragRate * dt);

        // 중성부력 상태에서 아주 느린 수직 속도는 정지 처리
        if (Mathf.Abs(netForce) < 1f && Mathf.Abs(vy) < 0.02f)
            vy = 0f;

        rb.linearVelocity = new Vector3(v.x, vy, v.z);
    }

    // 선체가 물에 잠긴 비율 (0 ~ 1). 중심이 수면에 있으면 0.5
    private float GetSubmergedFraction()
    {
        float centerAboveSurface = transform.position.y - sub.surfaceY;
        return Mathf.Clamp01(0.5f - centerAboveSurface / hullHeight);
    }

    // 중성부력(부력 = 중력)이 되는 탱크 수위
    private float GetNeutralFill()
    {
        float density = sub.GetWaterDensity(sub.depth);
        float neutralBallastMass = density * displacedVolume - hullDryMass;
        return Mathf.Clamp01(neutralBallastMass / (density * ballastCapacity));
    }
}