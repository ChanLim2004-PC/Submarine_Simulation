using UnityEditor;                                 // 에디터 전용 기능 (메뉴, 에셋 저장)
using UnityEditor.SceneManagement;                 // 씬 저장
using UnityEngine;                                 // 기본 Unity 타입
using UnityEngine.Rendering;                       // Volume, VolumeProfile
using UnityEngine.Rendering.Universal;             // Bloom, Vignette, ColorAdjustments

// 카메라 톤 — 3인칭 카메라용 Volume 프로필(ThirdPersonProfile)을 만들고 LightScene에 적용하는 에디터 도구
// 사용법: LightScene을 연 상태에서 메뉴 Light > 2. 카메라 톤 (ThirdPersonProfile)
public static class ThirdPersonProfileBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";                    // 적용할 테스트 씬
    const string ProfileFolder = "Assets/Settings/Light";                         // 빛 파트 설정 폴더
    const string ProfilePath = ProfileFolder + "/ThirdPersonProfile.asset";       // 만들 프로필 경로
    const string VolumeName = "Volume_ThirdPerson";                               // 씬에 놓을 Volume 오브젝트 이름

    [MenuItem("Light/2. 카메라 톤 (ThirdPersonProfile)")]
    public static void Build()
    {
        // Play 모드 중에는 에셋·씬을 바꿀 수 없으므로 중단
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("카메라 톤", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        // LightScene이 열려 있는지 확인 (다른 팀원 씬을 건드리지 않기 위해)
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("카메라 톤", "LightScene을 먼저 열어 주세요.\n(Project 창 Assets/Scenes/LightScene 더블클릭)", "확인");
            return;
        }

        // ── 1) 프로필 에셋 준비 ──
        if (!AssetDatabase.IsValidFolder(ProfileFolder))                         // Assets/Settings/Light 폴더가 없으면
            AssetDatabase.CreateFolder("Assets/Settings", "Light");               // 새로 만듦

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);  // 기존 프로필 찾기
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();          // 새 프로필 생성
            AssetDatabase.CreateAsset(profile, ProfilePath);                      // 에셋 파일로 저장
        }
        else
        {
            // 이미 있으면 덮어쓸지 확인 (인스펙터에서 조정한 값이 초기화되므로)
            if (!EditorUtility.DisplayDialog("카메라 톤", "ThirdPersonProfile이 이미 있습니다.\n시작값으로 초기화할까요?", "초기화", "취소"))
                return;
            foreach (var old in profile.components)                              // 기존 오버라이드들을
                Object.DestroyImmediate(old, true);                               // 에셋에서 삭제
            profile.components.Clear();                                           // 목록 비우기
        }

        // ── 2) 오버라이드 추가 (3인칭용: Vignette 약하게, Bloom, Color Adjustments) ──
        var bloom = AddOverride<Bloom>(profile);                                  // 밝은 부분이 번져 보이게
        bloom.threshold.Override(1.0f);                                           // 밝기 1 이상만 번짐 (발광 생물 Emission 1.5~4가 걸리도록)
        bloom.intensity.Override(0.6f);                                           // 번짐 세기
        bloom.scatter.Override(0.7f);                                             // 번짐 퍼지는 정도 (물속이라 넓게)

        var vignette = AddOverride<Vignette>(profile);                            // 화면 가장자리 어둡게
        vignette.intensity.Override(0.25f);                                       // 약하게 (1인칭 ROV 카메라보다 덜)
        vignette.smoothness.Override(0.4f);                                       // 경계를 부드럽게

        var color = AddOverride<ColorAdjustments>(profile);                       // 전체 색감 조정
        color.contrast.Override(8f);                                              // 대비 살짝 올림 (물속 뿌연 느낌 보정)
        color.saturation.Override(-8f);                                           // 채도 살짝 내림 (카메라 영상 느낌)

        EditorUtility.SetDirty(profile);                                          // 변경 표시
        AssetDatabase.SaveAssets();                                               // 디스크에 저장

        // ── 3) 씬에 Global Volume 배치 ──
        var go = GameObject.Find(VolumeName);                                     // 이미 있으면 재사용
        if (go == null) go = new GameObject(VolumeName);                          // 없으면 새로 만듦
        var volume = go.GetComponent<Volume>();
        if (volume == null) volume = go.AddComponent<Volume>();                   // Volume 컴포넌트 추가
        volume.isGlobal = true;                                                   // 위치와 상관없이 화면 전체에 적용
        volume.priority = 10f;                                                    // 기본 프로필보다 우선
        volume.weight = 1f;                                                       // 100% 적용 (나중에 ViewModeLighting이 이 값으로 시점 전환)
        volume.sharedProfile = profile;                                           // 방금 만든 프로필 연결

        // 씬 저장 후 Volume 선택 (인스펙터에서 바로 값 확인 가능)
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        Debug.Log($"[카메라 톤] {ProfilePath} 생성, {VolumeName} 배치 완료");
    }

    // 프로필에 오버라이드(Bloom 등)를 추가하고, 프로필 에셋 안에 함께 저장
    static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var comp = profile.Add<T>(true);                                          // 추가 + 모든 항목 "덮어쓰기" 켬
        comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;   // Project 창에 하위 에셋으로 따로 안 보이게
        AssetDatabase.AddObjectToAsset(comp, profile);                            // 프로필 파일 안에 저장
        return comp;
    }
}
