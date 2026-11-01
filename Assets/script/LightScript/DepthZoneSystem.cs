using System;                     // Action (이벤트)
using System.Collections.Generic;
using UnityEngine;                // 기본 Unity 타입
using UnityEngine.Rendering;      // AmbientMode
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 수심대 시스템
// 추적 대상(잠수정)의 표시 수심을 계산하고, 수심대에 맞춰 매 프레임 아래 값을 적용한다
//   - 전역 셰이더 변수: _UW_Sigma(감쇠), _UW_WaterColor(물색), _UW_Enabled(수중 감쇠 효과 켜짐)
//   - 주변광 세기, 태양(Directional Light) 세기
//   - 입자 밀도 (떠다니는 입자 시스템이 CurrentParticleDensity를 읽음)
// 경계 근처에서는 값을 부드럽게 섞어 색이 튀지 않게 한다
// 주의: UnderwaterFogPreview(테스트 도구)와 같은 변수를 쓰므로 한 씬에 같이 두지 않는다
//
// 프리팹으로 다른 씬에 놓을 수 있게 태양과 대상은 비어 있으면 알아서 찾고,
// 꺼지거나 씬을 저장할 때는 그 씬 원래의 햇빛/주변광으로 돌려 놓는다. 한 씬에 하나만 동작.
[ExecuteAlways]   // Play 하지 않아도 에디터에서 바로 적용
public class DepthZoneSystem : MonoBehaviour
{
    static readonly int SigmaId = Shader.PropertyToID("_UW_Sigma");            // 감쇠 변수 ID
    static readonly int WaterColorId = Shader.PropertyToID("_UW_WaterColor");  // 물색 변수 ID
    static readonly int EnabledId = Shader.PropertyToID("_UW_Enabled");        // 효과 켜짐 변수 ID (효과 토글 키도 이 값을 씀)

    static readonly List<DepthZoneSystem> enabledSystems = new List<DepthZoneSystem>();  // 중복 체크용

    // 씬에 있는 수심대 시스템 (다른 파트가 DepthZoneSystem.Instance로 찾아 쓰기 위함)
    public static DepthZoneSystem Instance { get; private set; }

    [Header("설정")]
    public DepthSettings depthSettings;          // 수심 좌표 (수면 y, depthScale)
    public UnderwaterProfile profile;            // 수심대별 값
    [Tooltip("수심을 잴 대상 (잠수정). 비어 있으면 메인 카메라")]
    public Transform target;                     // 추적 대상
    [Tooltip("수심에 따라 세기를 줄일 햇빛. 비워 두면 씬의 태양을 찾아서 씀")]
    public Light sun;                            // 태양

    // ── 다른 파트가 읽는 값 (인스펙터 아래쪽에 실시간 표시) ──
    public float CurrentDepth { get; private set; }              // 표시 수심 (m)
    public DepthZone CurrentZone { get; private set; }           // 현재 수심대
    public event Action<DepthZone> OnZoneChanged;                // 수심대가 바뀔 때 알림 (계기판·생물 배치용)

    public Color CurrentWaterColor { get; private set; }         // 지금 적용 중인 물색
    public Vector3 CurrentSigma { get; private set; }            // 지금 적용 중인 감쇠 계수
    public float CurrentAmbient { get; private set; }            // 지금 적용 중인 주변광 세기
    public float CurrentSunIntensity { get; private set; }       // 지금 적용 중인 태양 세기
    public float CurrentParticleDensity { get; private set; }    // 지금 적용할 입자 수
    public bool IsDuplicate => isDuplicate;
    public Light ActiveSun => activeSun;

    bool zoneInitialized;                        // 첫 프레임에는 OnZoneChanged를 보내지 않기 위한 표시
    bool isDuplicate;
    bool warnedNoTarget;
    Light activeSun;

    // 꺼질 때 되돌릴 원래 값
    bool hasOriginals;
    float originalSunIntensity;
    bool originalSunEnabled;
    AmbientMode originalAmbientMode;
    Color originalAmbientLight;
    float originalAmbientIntensity;

    void OnEnable()
    {
        // 같은 씬에 이미 켜진 게 있으면 이건 쉰다
        enabledSystems.Add(this);
        isDuplicate = enabledSystems.Count > 1;
        if (isDuplicate)
        {
            Debug.LogWarning($"[수심대] 이 씬에 수심대 시스템이 이미 있습니다 ('{enabledSystems[0].name}'). '{name}'은(는) 동작하지 않습니다. 하나만 남기고 지워 주세요.", this);
            return;
        }

        Instance = this;                         // 다른 스크립트가 찾을 수 있게 등록
        zoneInitialized = false;                 // 수심대 다시 판정
        warnedNoTarget = false;
        activeSun = ResolveSun();
        SaveOriginals();
        CheckSceneSetup();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
#endif
        Apply();
    }

    void Update() { if (!isDuplicate) Apply(); }                            // 매 프레임 적용 (에디터에서도)
    void OnValidate() { if (isActiveAndEnabled && !isDuplicate) Apply(); }   // 인스펙터 값을 바꾸면 바로 적용

    void OnDisable()
    {
        enabledSystems.Remove(this);
        if (isDuplicate) { isDuplicate = false; return; }

#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
#endif
        if (Instance == this) Instance = null;               // 등록 해제
        Shader.SetGlobalFloat(EnabledId, 0f);                // 수중 감쇠 효과 끄기 → 원래 화면
        RestoreOriginals();
    }

#if UNITY_EDITOR
    // 저장하는 동안만 원래 조명으로 돌려 둔다. 안 그러면 수심 때문에 바뀐 값이 씬 파일에 들어간다
    void OnSceneSaving(Scene scene, string path) { if (scene == gameObject.scene) RestoreOriginals(); }
    void OnSceneSaved(Scene scene) { if (scene == gameObject.scene) Apply(); }
#endif

    void Apply()
    {
        if (profile == null || depthSettings == null)        // 설정이 비어 있으면 효과를 끄고 중단
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            return;
        }

        // 1) 추적 대상의 표시 수심 계산
        Transform t = target != null ? target : (Camera.main != null ? Camera.main.transform : null);
        if (t == null)                                       // 대상도 카메라도 없으면 효과 끄고 한 번만 알림
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            if (!warnedNoTarget)
            {
                Debug.LogWarning("[수심대] 수심을 잴 대상이 없습니다. Target에 잠수정을 넣거나, 카메라에 'MainCamera' 태그를 붙여 주세요.", this);
                warnedNoTarget = true;
            }
            return;
        }
        warnedNoTarget = false;
        CurrentDepth = depthSettings.ToDisplayDepth(t.position.y);

        // 2) 경계 블렌드 비율 (0 = 위 구역 값, 1 = 아래 구역 값)
        float t1 = BlendAt(profile.photicBottom);            // 유광층 → 약광층
        float t2 = BlendAt(profile.twilightBottom);          // 약광층 → 무광층

        // 3) 세 구역 값을 차례로 섞음 (색은 리니어 공간에서 섞어야 밝기가 자연스러움)
        var p = profile.photic; var w = profile.twilight; var m = profile.midnight;
        Color colorLinear = Color.Lerp(Color.Lerp(p.waterColor.linear, w.waterColor.linear, t1), m.waterColor.linear, t2);
        CurrentWaterColor = colorLinear.gamma;                                                    // 표시용 (인스펙터 색)
        CurrentSigma = Vector3.Lerp(Vector3.Lerp(p.sigma, w.sigma, t1), m.sigma, t2);            // 감쇠
        CurrentAmbient = Mathf.Lerp(Mathf.Lerp(p.ambient, w.ambient, t1), m.ambient, t2);        // 주변광
        CurrentParticleDensity = Mathf.Lerp(Mathf.Lerp(p.particleDensity, w.particleDensity, t1), m.particleDensity, t2); // 입자 수

        // 4) 햇빛: 수면에서 최대, 유광층 바닥에서 0 (그 아래로는 계속 0)
        CurrentSunIntensity = profile.sunSurfaceIntensity * Mathf.Clamp01(1f - CurrentDepth / Mathf.Max(1f, profile.photicBottom));

        // 5) 적용 — 수중 감쇠 셰이더
        Shader.SetGlobalVector(SigmaId, CurrentSigma);                    // 감쇠
        Shader.SetGlobalVector(WaterColorId, colorLinear);                // 물색 (리니어)
        Shader.SetGlobalFloat(EnabledId, 1f);                             // 효과 켜기

        // 6) 적용 — 주변광 (단색 모드로 바꿔 세기를 바로 반영)
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = profile.ambientColor * CurrentAmbient;

        // 7) 적용 — 햇빛 (0이면 꺼서 그림자 계산도 생략)
        if (sun != null && activeSun != sun) activeSun = sun;
        if (activeSun != null)
        {
            activeSun.intensity = CurrentSunIntensity;
            activeSun.enabled = CurrentSunIntensity > 0.001f;
        }

        // 8) 수심대 판정 (경계 기준, 블렌드 없이) + 바뀌면 알림
        DepthZone zone = CurrentDepth < profile.photicBottom ? DepthZone.Photic
                       : CurrentDepth < profile.twilightBottom ? DepthZone.Twilight
                       : DepthZone.Midnight;
        if (!zoneInitialized)
        {
            CurrentZone = zone;                  // 첫 판정은 알림 없이 저장만
            zoneInitialized = true;
        }
        else if (zone != CurrentZone)
        {
            CurrentZone = zone;
            OnZoneChanged?.Invoke(zone);         // 구독한 스크립트에 알림
        }
    }

    // 경계(boundary) 앞뒤 blendWidth 구간에서 0 → 1로 부드럽게 바뀌는 값
    float BlendAt(float boundary)
    {
        float half = Mathf.Max(0.01f, profile.blendWidth) * 0.5f;                       // 블렌드 폭의 절반
        float x = Mathf.InverseLerp(boundary - half, boundary + half, CurrentDepth);   // 구간 안 위치 (0~1)
        return Mathf.SmoothStep(0f, 1f, x);                                             // 양 끝을 완만하게
    }

    // 직접 지정한 것 > Lighting 창의 Sun Source > 씬의 첫 Directional Light 순서로 찾는다
    Light ResolveSun()
    {
        if (sun != null) return sun;
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional) return light;
        return null;
    }

    void SaveOriginals()
    {
        if (activeSun != null)
        {
            originalSunIntensity = activeSun.intensity;
            originalSunEnabled = activeSun.enabled;
        }
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        hasOriginals = true;
    }

    void RestoreOriginals()
    {
        if (!hasOriginals) return;
        if (activeSun != null)
        {
            activeSun.intensity = originalSunIntensity;
            activeSun.enabled = originalSunEnabled;
        }
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
    }

    // 다른 사람 씬일 수 있어서 직접 고치지 않고 Console로만 알려 준다
    void CheckSceneSetup()
    {
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var camData) && !camData.renderPostProcessing)
            Debug.LogWarning($"[수심대] 카메라 '{cam.name}'의 Post Processing이 꺼져 있어 카메라 톤(가장자리 어둡게·Bloom)이 보이지 않습니다. 카메라 Inspector > Rendering > Post Processing을 체크해 주세요.", cam);

        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var volume in root.GetComponentsInChildren<Volume>(true))
                if (volume.isGlobal && !volume.transform.IsChildOf(transform))
                    Debug.Log($"[수심대] 다른 Global Volume '{volume.name}'(Priority {volume.priority})이 있습니다. 같은 효과 항목은 Priority가 높은 쪽 값이 적용됩니다 (빛 파트 카메라 톤 = 10).", volume);
    }
}
