using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    public enum CameraMode
    {
        FreeOrbit,  // 우클릭 드래그로 공전, 잠수함 회전과 무관
        Follow      // 잠수함 뒤에 고정, 잠수함 선회각만큼 같이 회전
    }

    [Header("3인칭 모드")]
    public CameraMode mode = CameraMode.FreeOrbit;

    // 3인칭 카메라 모드 전환 키
    public Key modeToggleKey = Key.O;

    [Header("대상")]
    public Transform target;                                // 따라갈 오브젝트
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);  // 중심점 높이 보정

    [Header("거리")]
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float zoomSpeed = 50f;

    [Header("회전")]
    public float rotateSpeed = 3f;   // 마우스 감도
    public float minPitch = -30f;    // 아래로 볼 수 있는 한계
    public float maxPitch = 70f;     // 위로 볼 수 있는 한계

    [Header("1인칭")]
    // 시점(3인칭/1인칭) 관리 스크립트 (잠수함에 붙어 있음)
    public SubmarineViewMode viewMode;

    // 1인칭 카메라 위치 (잠수함의 자식 빈 오브젝트, 앞/z 방향이 정면)
    public Transform firstPersonAnchor;

    // 1인칭에서 둘러보는 감도
    public float firstPersonLookSpeed = 3f;

    // 1인칭에서 둘러볼 수 있는 최대 각도 (정면 기준)
    public float maxLookYaw = 90f;
    public float maxLookPitch = 60f;

    // 1인칭일 때 숨길 오브젝트 (예: 카메라를 가리는 선체 모델)
    public GameObject[] hideInFirstPerson;

    // 구 Input Manager의 Mouse X/Y와 비슷한 감도로 맞추는 계수
    const float MouseDeltaScale = 0.1f;

    // 휠 한 칸(보통 120)이 구 방식의 0.12 정도가 되도록 맞추는 계수
    const float ScrollScale = 0.001f;

    // FreeOrbit: 월드 기준 yaw / Follow: 잠수함 기준 상대 yaw
    float yaw;
    float pitch = 20f;

    // 1인칭 둘러보기 각도 (앵커 기준)
    float lookYaw;
    float lookPitch;

    bool wasFirstPerson;

    void Start()
    {
        // 시작 시 현재 카메라 각도를 이어받음
        yaw = transform.eulerAngles.y;

        if (mode == CameraMode.Follow && target != null)
            yaw = Mathf.DeltaAngle(target.eulerAngles.y, yaw);

        if (viewMode == null && target != null)
            viewMode = target.GetComponentInParent<SubmarineViewMode>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 시점이 바뀌었을 때 처리
        bool firstPerson = viewMode != null && viewMode.mode == ViewMode.FirstPerson;
        if (firstPerson != wasFirstPerson)
            OnViewChanged(firstPerson);

        if (firstPerson)
        {
            UpdateFirstPerson();
            return;
        }

        UpdateThirdPerson();
    }

    #region 3인칭

    void UpdateThirdPerson()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // 카메라 모드 전환 (O 키, 3인칭에서만)
        if (keyboard != null && keyboard[modeToggleKey].wasPressedThisFrame)
            ToggleMode();

        if (mouse != null && !SubmarineMinimap.IsMapOpen)
        {
            // 우클릭 누르고 있는 동안만 회전
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * MouseDeltaScale * rotateSpeed;
                pitch -= delta.y * MouseDeltaScale * rotateSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            // 마우스 휠로 줌
            float scroll = mouse.scroll.ReadValue().y;
            distance -= scroll * ScrollScale * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // 추적 모드에서는 잠수함의 Y 회전을 더해 같이 돌게 함
        float worldYaw = yaw;
        if (mode == CameraMode.Follow)
            worldYaw += target.eulerAngles.y;

        // 회전값 기준으로 타겟 뒤쪽에 카메라 배치
        Quaternion rotation = Quaternion.Euler(pitch, worldYaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        transform.position = pivot - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }

    void ToggleMode()
    {
        if (mode == CameraMode.FreeOrbit)
        {
            // Follow로 전환: 잠수함 바로 뒤로 순간이동
            yaw = 0f;
            mode = CameraMode.Follow;
        }
        else
        {
            // FreeOrbit으로 전환: 현재 화면 각도 유지
            yaw = yaw + target.eulerAngles.y;
            mode = CameraMode.FreeOrbit;
        }
    }

    #endregion

    #region 1인칭

    // 카메라를 앵커에 붙이고 우클릭 드래그로 둘러보기
    void UpdateFirstPerson()
    {
        Transform anchor = firstPersonAnchor != null ? firstPersonAnchor : target;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed && !SubmarineMinimap.IsMapOpen)
        {
            Vector2 delta = mouse.delta.ReadValue();
            lookYaw += delta.x * MouseDeltaScale * firstPersonLookSpeed;
            lookPitch -= delta.y * MouseDeltaScale * firstPersonLookSpeed;

            lookYaw = Mathf.Clamp(lookYaw, -maxLookYaw, maxLookYaw);
            lookPitch = Mathf.Clamp(lookPitch, -maxLookPitch, maxLookPitch);
        }

        transform.SetPositionAndRotation(
            anchor.position,
            anchor.rotation * Quaternion.Euler(lookPitch, lookYaw, 0f));
    }

    // 시점이 바뀔 때 한 번 호출
    void OnViewChanged(bool firstPerson)
    {
        wasFirstPerson = firstPerson;

        // 둘러보기 각도는 정면에서 시작
        lookYaw = 0f;
        lookPitch = 0f;

        // 1인칭에서 가릴 오브젝트 처리
        foreach (GameObject go in hideInFirstPerson)
        {
            if (go != null)
                go.SetActive(!firstPerson);
        }
    }

    #endregion
}