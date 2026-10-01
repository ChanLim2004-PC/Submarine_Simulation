using UnityEngine;   // 기본 Unity 타입

// L1 수중 감쇠 확인용 (3단계 임시 도구)
// 인스펙터에서 정한 σ·물색을 전역 셰이더 변수로 넘긴다.
// 4단계에서 DepthZoneSystem(수심대 시스템)이 같은 일을 하게 되면 이 컴포넌트는 꺼 두거나 지운다.
[ExecuteAlways]   // Play 하지 않아도 에디터에서 바로 적용
public class UnderwaterFogPreview : MonoBehaviour
{
    static readonly int SigmaId = Shader.PropertyToID("_UW_Sigma");            // 셰이더 변수 이름 → 숫자 ID (빠른 접근용)
    static readonly int WaterColorId = Shader.PropertyToID("_UW_WaterColor");  // 물색 변수 ID
    // 효과 켜짐 변수 ID (1 = 켜짐, 0 = 꺼짐)
    // 이후 DepthZoneSystem(L3)과 L9 효과 토글 키도 이 변수로 효과를 켜고 끈다
    static readonly int EnabledId = Shader.PropertyToID("_UW_Enabled");

    [Tooltip("채널별 감쇠 계수 σ (R, G, B) /m. 클수록 그 색이 빨리 사라짐")]
    public Vector3 sigma = new Vector3(0.45f, 0.09f, 0.04f);                   // 유광층 시작값 (명세 7장)

    [Tooltip("멀리 있는 물체가 묻히는 물의 색")]
    public Color waterColor = new Color32(0x2A, 0x7F, 0xB0, 0xFF);             // 유광층 물색 #2A7FB0

    void Update() => Apply();       // 매 프레임 적용 (에디터에서도)
    void OnValidate() => Apply();   // 인스펙터 값을 바꾸는 즉시 적용

    void Apply()
    {
        if (!isActiveAndEnabled) return;                                       // 꺼져 있으면 아무것도 안 함
        Shader.SetGlobalVector(SigmaId, sigma);                                // σ 전달
        Shader.SetGlobalVector(WaterColorId, waterColor.linear);              // 물색 전달 (화면 계산용 리니어 색으로 직접 변환)
        Shader.SetGlobalFloat(EnabledId, 1f);                                  // 효과 켜기
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);                                  // 효과 끄기 → 원래 화면 (다른 씬으로 가거나 컴포넌트를 끌 때)
        Shader.SetGlobalVector(SigmaId, Vector3.zero);                         // 감쇠값도 0으로 정리
    }
}
