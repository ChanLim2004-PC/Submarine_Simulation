using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SubmarineController : MonoBehaviour
{
    #region 인스펙터 설정

    [Header("수심")]
    // 수면의 Y 좌표
    public float surfaceY = 0f;

    [Header("물리")]
    // 잠수함의 앞 넓이 a x b (m^2)
    public float frontalArea = 6f;

    // 2차 항력 계수 (v²에 비례, 고속에서 지배적), 일반적으로 0.02 ~ 0.07
    public float dragCoefficient = 0.05f;

    // 선형 항력 계수 (1/s, v에 비례, 저속에서 지배적). 클수록 저속에서 빨리 멈춤
    public float linearDrag = 0.3f;

    // 이 속력 미만이고 추진 입력이 없으면 정지 처리 (m/s)
    public float stopSpeedThreshold = 0.2f;

    [Header("추진력")]
    // 추진 가속도 (m/s²)
    public float thrustAcceleration = 3f;

    [Header("선회")]
    // 최대 선회 속도 (deg/s)
    public float turnSpeed = 20f;

    // 이 속력 이상에서 최대 선회 속도 적용 (m/s)
    public float maxTurnSpeedAtVelocity = 4f;

    // 정지 상태에서 선회할 수 있는 정도 (0 = 정지 시 선회 불가)
    [Range(0f, 1f)]
    public float minimumTurnFactor = 0f;

    [Header("좌우 미끄러짐")]
    // 옆으로 미끄러지는 속도를 줄이는 정도 (클수록 직진성 강화)
    public float lateralDamping = 4f;

    [Header("제동")]
    // P 키 제동 감속도 (m/s²)
    public float brakingDeceleration = 2f;

    [Header("크루즈")]
    // 이 키를 누를 때마다 자동 직진이 켜지고 꺼짐
    public Key cruiseToggleKey = Key.Backquote;

    // P(제동)를 누르면 크루즈를 자동으로 해제할지 여부
    public bool cancelCruiseOnBrake = true;

    // 현재 크루즈 활성화 여부
    public bool isCruising = false;

    [Header("상태 (확인용)")]
    // 현재 속력 (m/s)
    public float speed = 0f;

    #endregion

    #region 내부 변수

    private Rigidbody rb;

    private float moveInput;   // 전진/후진 입력 (-1 ~ 1)
    private float turnInput;   // 좌/우 선회 입력 (-1 ~ 1)
    private bool isBraking;    // P 키 제동 중 여부

    #endregion

    #region 프로퍼티

    // y 위치로 수심을 결정 (수면 위에서는 0)
    public float depth
    {
        get { return Mathf.Max(0f, surfaceY - transform.position.y); }
    }

    // 제동 중인지 (HUD 표시용)
    public bool IsBraking
    {
        get { return isBraking; }
    }

    // 수평 속력 (m/s)
    public float CurrentSpeed
    {
        get
        {
            Vector3 v = rb.linearVelocity;
            return new Vector3(v.x, 0f, v.z).magnitude;
        }
    }

    // 수직 속도 (m/s, 양수 = 상승)
    public float CurrentVerticalSpeed
    {
        get { return rb.linearVelocity.y; }
    }

    #endregion

    #region 유니티 메시지

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 중력과 기본 감쇠는 직접 계산하므로 끔
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 2f;
    }

    // 입력은 Update에서 읽고, 물리 계산은 FixedUpdate에서 처리
    void Update()
    {
        moveInput = 0f;
        turnInput = 0f;

        if (Keyboard.current == null)
        {
            isBraking = false;
            return;
        }

        // 크루즈 토글 (눌린 프레임에 한 번만)
        if (Keyboard.current[cruiseToggleKey].wasPressedThisFrame)
            isCruising = !isCruising;

        // 크루즈 중에는 W를 누르고 있는 것과 같음
        if (isCruising)
            moveInput += 1f;

        if (Keyboard.current.wKey.isPressed)
            moveInput += 1f;

        if (Keyboard.current.sKey.isPressed)
            moveInput -= 1f;

        if (Keyboard.current.aKey.isPressed)
            turnInput -= 1f;

        if (Keyboard.current.dKey.isPressed)
            turnInput += 1f;

        // 크루즈 중 W를 같이 눌러도 2배가 되지 않도록 제한
        moveInput = Mathf.Clamp(moveInput, -1f, 1f);

        // P를 누르는 동안에만 제동
        isBraking = Keyboard.current.pKey.isPressed;

        // 제동하면 크루즈 해제
        if (isBraking && cancelCruiseOnBrake)
            isCruising = false;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 수직 속도는 SubmarineBuoyancy가 담당하므로 분리해서 보존
        Vector3 fullVelocity = rb.linearVelocity;
        float verticalVelocity = fullVelocity.y;

        // 이 스크립트는 수평 속도만 계산
        Vector3 velocity = new Vector3(fullVelocity.x, 0f, fullVelocity.z);

        // 제동 중에는 추진 입력 차단
        float effectiveMoveInput = isBraking ? 0f : moveInput;

        velocity = ApplyThrust(velocity, effectiveMoveInput, dt);
        velocity = ApplyDrag(velocity, dt);
        velocity = ApplyLateralDamping(velocity, dt);

        if (isBraking)
            velocity = ApplyBraking(velocity, dt);

        velocity = ApplyStopThreshold(velocity, effectiveMoveInput);

        // 수평 속도 + 보존해 둔 수직 속도를 합쳐 적용
        rb.linearVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);
        speed = velocity.magnitude;   // 수평 속력

        ApplyTurn(dt);
    }

    #endregion

    #region 물리 계산

    // 1. 추진력: 선체 전방으로 가속
    private Vector3 ApplyThrust(Vector3 velocity, float input, float dt)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        return velocity + forward * input * thrustAcceleration * dt;
    }

    // 2. 항력: 선형 + 2차 항력을 암시적 적분으로 적용 (dt가 커도 속도가 반전/발산하지 않음)
    private Vector3 ApplyDrag(Vector3 velocity, float dt)
    {
        float currentSpeed = velocity.magnitude;

        if (currentSpeed <= 0f)
            return velocity;

        float waterDensity = GetWaterDensity(depth);

        // 2차 항력 계수 (1/m)
        float quadraticDrag =
            0.5f * waterDensity * dragCoefficient * frontalArea / rb.mass;

        float dragRate = linearDrag + quadraticDrag * currentSpeed;

        return velocity / (1f + dragRate * dt);
    }

    // 3. 좌우 미끄러짐 감쇠: 로컬 X축 속도만 지수적으로 줄임
    private Vector3 ApplyLateralDamping(Vector3 velocity, float dt)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        localVelocity.x *= Mathf.Exp(-lateralDamping * dt);

        return transform.TransformDirection(localVelocity);
    }

    // 4. P 키 제동: 일정 감속도로 0까지 감소
    private Vector3 ApplyBraking(Vector3 velocity, float dt)
    {
        return Vector3.MoveTowards(
            velocity,
            Vector3.zero,
            brakingDeceleration * dt
        );
    }

    // 5. 정지 처리: 추진 입력이 없을 때 매우 낮은 수평 속도는 0으로 (수직 속도는 유지)
    private Vector3 ApplyStopThreshold(Vector3 velocity, float input)
    {
        if (Mathf.Approximately(input, 0f) &&
            velocity.magnitude < stopSpeedThreshold)
        {
            return Vector3.zero;
        }

        return velocity;
    }

    // 6. 선회: 속력이 빠를수록 선회 속도 증가
    private void ApplyTurn(float dt)
    {
        // 현재 속력에 따른 선회 비율 (0 ~ 1)
        float turnFactor = Mathf.Clamp01(speed / maxTurnSpeedAtVelocity);

        // 최소 선회 비율 적용
        turnFactor = Mathf.Lerp(minimumTurnFactor, 1f, turnFactor);

        float currentTurnSpeed = turnSpeed * turnFactor;
        float turnAmount = turnInput * currentTurnSpeed * dt;

        Quaternion rotation = Quaternion.Euler(0f, turnAmount, 0f);
        rb.MoveRotation(rb.rotation * rotation);
    }

    #endregion

    #region 공개 함수

    // 수심에 따른 물의 밀도 (kg/m³)
    public float GetWaterDensity(float depth)
    {
        return 1020f + Mathf.Max(0f, depth) * 0.003f;
    }

    #endregion
}