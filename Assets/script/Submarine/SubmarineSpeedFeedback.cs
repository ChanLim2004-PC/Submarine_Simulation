using UnityEngine;

// 속도에 따라 시야각, 카메라 흔들림, 입자 양을 바꿔 속도감을 줌.
// OrbitCamera가 카메라 위치를 정한 뒤 실행되도록 순서를 늦춤
[DefaultExecutionOrder(100)]
public class SubmarineSpeedFeedback : MonoBehaviour
{
    [Header("참조 (비워두면 자동 탐색)")]
    public SubmarineController submarine;
    public Camera targetCamera;

    [Header("기준")]
    // 이 속력(m/s)에서 효과가 최대가 됨. 실제 최고속도에 맞춰 조절
    public float maxSpeedReference = 6f;

    // 효과가 변하는 부드러움 (클수록 빠르게 따라감)
    public float smoothing = 3f;

    [Header("시야각")]
    // 최대 속도에서 늘어나는 시야각 (deg)
    public float maxFovAdd = 10f;

    [Header("카메라 흔들림")]
    public bool enableShake = true;

    // 최대 속도에서의 흔들림 크기 (m)
    public float shakeAmplitude = 0.03f;
    public float shakeFrequency = 18f;

    [Header("입자 (선택)")]
    // 카메라 주변에 둔 파티클 (비워두면 건너뜀)
    public ParticleSystem particles;

    // 정지 / 최고속도에서의 초당 입자 수
    public float minEmission = 20f;
    public float maxEmission = 120f;

    private float baseFov;
    private float smoothedT;

    void Awake()
    {
        if (submarine == null)
            submarine = FindAnyObjectByType<SubmarineController>();

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (submarine == null || targetCamera == null)
        {
            Debug.LogError("SubmarineSpeedFeedback: 잠수함 또는 카메라를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        // 시작할 때의 시야각을 기준으로 사용
        baseFov = targetCamera.fieldOfView;
    }

    void LateUpdate()
    {
        // 수평 속력 기준 (0 ~ 1)
        float t = Mathf.Clamp01(submarine.CurrentSpeed / maxSpeedReference);
        smoothedT = Mathf.Lerp(smoothedT, t, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        // 시야각
        targetCamera.fieldOfView = baseFov + maxFovAdd * smoothedT;

        // 카메라 흔들림 (OrbitCamera가 매 프레임 위치를 다시 정하므로 누적되지 않음)
        if (enableShake && smoothedT > 0.01f)
        {
            float time = Time.time * shakeFrequency;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(time, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, time) - 0.5f,
                0f) * 2f * shakeAmplitude * smoothedT;

            targetCamera.transform.position +=
                targetCamera.transform.TransformDirection(offset);
        }

        // 입자 방출량
        if (particles != null)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Lerp(minEmission, maxEmission, smoothedT);
        }
    }
}