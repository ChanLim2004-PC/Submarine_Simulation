using UnityEditor;                                 // 에디터 전용 기능 (메뉴, 에셋 저장)
using UnityEditor.SceneManagement;                 // 씬 저장
using UnityEngine;                                 // 기본 Unity 타입

// 수심대 시스템 — 설정 에셋 생성 + LightScene에 DepthZoneSystem 배치
// 사용법: LightScene을 연 상태에서 메뉴 Light > 4. 수심대 시스템 (DepthZoneSystem)
public static class DepthZoneBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";                    // 테스트 씬
    const string SettingsFolder = "Assets/Settings/Light";                        // 빛 파트 설정 폴더
    const string DepthSettingsPath = SettingsFolder + "/DepthSettings.asset";     // 수심 좌표 설정
    const string ProfilePath = SettingsFolder + "/UnderwaterProfile.asset";       // 수심대별 값

    [MenuItem("Light/4. 수심대 시스템 (DepthZoneSystem)")]
    public static void Build()
    {
        // Play 모드 중에는 중단
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("수심대", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        // 프리팹이 생긴 뒤에 여기서 또 만들면 수심대 시스템이 두 개가 된다
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnderwaterSystemBuilder.PrefabPath) != null)
        {
            EditorUtility.DisplayDialog("수심대", "UnderwaterSystem 프리팹이 이미 있습니다.\n수심대 시스템은 프리팹에 들어 있으니 메뉴 5를 사용해 주세요.", "확인");
            return;
        }

        // LightScene이 열려 있는지 확인
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("수심대", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        // ── 1) 설정 에셋 (이미 있으면 그대로 사용 — 인스펙터에서 조정한 값 유지) ──
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Light");
        var depthSettings = GetOrCreate<DepthSettings>(DepthSettingsPath);       // 수심 좌표 (기본: 수면 y=0, 배율 5)
        var profile = GetOrCreate<UnderwaterProfile>(ProfilePath);               // 수심대별 값 (기본: 명세 시작값)
        AssetDatabase.SaveAssets();

        // ── 2) 테스트 도구(UnderwaterFogPreview) 제거 — 같은 셰이더 변수를 덮어쓰므로 ──
        UnderwaterFogPreview preview;
        while ((preview = Object.FindAnyObjectByType<UnderwaterFogPreview>(FindObjectsInactive.Include)) != null)  // 꺼진 것까지 하나씩 찾아서
        {
            Debug.Log($"[수심대] '{preview.gameObject.name}' 제거 (수심대 시스템이 대신함)");
            Object.DestroyImmediate(preview.gameObject);                         // 오브젝트째 삭제
        }

        // ── 3) 씬 오브젝트 찾기 ──
        var sub = GameObject.Find("Submarine_Proxy");                            // 수심 추적 대상
        var sunGO = GameObject.Find("Sun");                                      // 햇빛
        var targets = GameObject.Find("TestTargets");                            // 색 큐브 묶음

        // 확인용: 색 큐브를 잠수정 자식으로 → 잠수정을 내려도 카메라와 큐브 거리(2~40m)가 유지됨
        if (sub != null && targets != null && targets.transform.parent != sub.transform)
            targets.transform.SetParent(sub.transform, true);                    // 월드 위치는 그대로 두고 부모만 변경

        // ── 4) DepthZoneSystem 배치 ──
        var go = GameObject.Find("DepthZoneSystem");
        if (go == null) go = new GameObject("DepthZoneSystem");
        var sys = go.GetComponent<DepthZoneSystem>();
        if (sys == null) sys = go.AddComponent<DepthZoneSystem>();
        sys.depthSettings = depthSettings;                                       // 설정 연결
        sys.profile = profile;
        sys.target = sub != null ? sub.transform : null;                         // 잠수정 (없으면 메인 카메라 사용)
        sys.sun = sunGO != null ? sunGO.GetComponent<Light>() : RenderSettings.sun;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;                                         // 인스펙터에서 바로 상태 확인
        Debug.Log("[수심대] DepthSettings·UnderwaterProfile 생성, DepthZoneSystem 배치 완료");
    }

    // 에셋이 있으면 불러오고, 없으면 기본값으로 새로 만듦
    static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();                        // 코드에 적힌 기본값으로 생성
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }
}
