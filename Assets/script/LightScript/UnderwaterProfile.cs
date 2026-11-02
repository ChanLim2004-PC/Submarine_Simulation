using System;        // [Serializable]
using UnityEngine;   // 기본 Unity 타입

// 수심대 구분 (표시 수심 기준)
public enum DepthZone
{
    Photic,     // 유광층 0 ~ 200m: 햇빛이 들어옴
    Twilight,   // 약광층 200 ~ 1,000m: 빛이 거의 사라짐
    Midnight    // 무광층 1,000m ~: 완전한 어둠
}

// 수심대별 물색·감쇠·밝기 설정 (DepthZoneSystem이 읽어서 적용)
[CreateAssetMenu(fileName = "UnderwaterProfile", menuName = "Light/Underwater Profile")]
public class UnderwaterProfile : ScriptableObject
{
    // 한 수심대의 값 묶음
    [Serializable]
    public class ZoneSettings
    {
        [Tooltip("멀리 있는 물체가 묻히는 물의 색")]
        public Color waterColor;                 // 물색
        [Tooltip("채널별 감쇠 계수 σ (R, G, B) /m. 클수록 그 색이 빨리 사라짐")]
        public Vector3 sigma;                    // 감쇠 계수
        [Tooltip("주변광 세기 (0 = 완전히 어두움)")]
        public float ambient;                    // 주변광 세기
        [Tooltip("떠다니는 입자 개수 (입자 시스템이 읽음)")]
        public int particleDensity;              // 입자 밀도

        public ZoneSettings(Color color, Vector3 sigma, float ambient, int particles)
        {
            waterColor = color; this.sigma = sigma; this.ambient = ambient; particleDensity = particles;
        }
    }

    [Header("수심대별 값")]
    public ZoneSettings photic = new ZoneSettings(new Color32(0x2A, 0x7F, 0xB0, 0xFF), new Vector3(0.45f, 0.09f, 0.04f), 1.0f, 800);      // 유광층
    public ZoneSettings twilight = new ZoneSettings(new Color32(0x0C, 0x3A, 0x5C, 0xFF), new Vector3(0.45f, 0.09f, 0.04f), 0.25f, 1500);  // 약광층
    public ZoneSettings midnight = new ZoneSettings(new Color32(0x03, 0x10, 0x1C, 0xFF), new Vector3(0.40f, 0.08f, 0.035f), 0.03f, 2500); // 무광층

    [Header("경계 (표시 수심 m)")]
    public float photicBottom = 200f;            // 유광층 → 약광층 경계
    public float twilightBottom = 1000f;         // 약광층 → 무광층 경계
    [Tooltip("경계 앞뒤로 값을 부드럽게 섞는 폭 (m). 예: 100이면 150~250m에서 서서히 바뀜")]
    public float blendWidth = 100f;              // 경계 블렌드 폭

    [Header("햇빛·주변광")]
    [Tooltip("수면(0m)에서의 태양 세기. 유광층 바닥(photicBottom)에서 0이 됨")]
    public float sunSurfaceIntensity = 1f;       // 수면 태양 세기
    [Tooltip("물속 주변광의 색 (주변광 세기를 곱해서 사용)")]
    public Color ambientColor = new Color(0.55f, 0.7f, 0.8f);   // 물속 주변광 색 (푸른 회색)

    [Header("커스틱스 (바닥에 일렁이는 물결 빛)")]
    [Tooltip("밝기. 수면 근처 기준이며 햇빛과 같이 유광층 바닥에서 0이 됨")]
    [Range(0f, 3f)] public float causticIntensity = 0.8f;
    [Tooltip("무늬 한 칸의 크기 (m)")]
    [Range(0.2f, 10f)] public float causticScale = 1.5f;
    [Tooltip("꿈틀거리는 속도")]
    [Range(0f, 3f)] public float causticSpeed = 0.5f;
    [Tooltip("카메라에서 이 거리(m)를 넘으면 무늬가 사라짐")]
    [Range(5f, 100f)] public float causticFadeDistance = 30f;

    [Header("햇빛 줄기")]
    [Tooltip("밝기. 커스틱스처럼 유광층 바닥에서 0이 됨")]
    [Range(0f, 5f)] public float shaftIntensity = 1f;
    [Tooltip("카메라에서 이 거리(m)까지만 계산")]
    [Range(5f, 80f)] public float shaftDistance = 40f;
    [Tooltip("줄기 사이 간격 (m)")]
    [Range(0.5f, 20f)] public float shaftScale = 4f;

    [Header("일렁임")]
    [Tooltip("화면이 흔들리는 정도. 0이면 끔")]
    [Range(0f, 0.01f)] public float distortionStrength = 0.003f;
    [Tooltip("물결 촘촘한 정도")]
    [Range(1f, 30f)] public float distortionScale = 6f;
    [Range(0f, 3f)] public float distortionSpeed = 0.6f;
}
