using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// UnderwaterSystem 프리팹(수심대 시스템 + 3인칭 카메라 톤)을 만들고 LightScene에 배치, MainScene 뼈대 생성. 메뉴: Light > 5
public static class UnderwaterSystemBuilder
{
    const string LightScenePath = "Assets/Scenes/LightScene.unity";
    const string MainScenePath = "Assets/Scenes/MainScene.unity";
    const string PrefabFolder = "Assets/Prefabs/Light";
    public const string PrefabPath = PrefabFolder + "/UnderwaterSystem.prefab";
    const string DepthSettingsPath = "Assets/Settings/Light/DepthSettings.asset";
    const string ProfilePath = "Assets/Settings/Light/UnderwaterProfile.asset";
    const string ToneProfilePath = "Assets/Settings/Light/ThirdPersonProfile.asset";

    [MenuItem("Light/5. UnderwaterSystem 프리팹 + MainScene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("UnderwaterSystem", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        var lightScene = EditorSceneManager.GetActiveScene();
        if (lightScene.path != LightScenePath)
        {
            EditorUtility.DisplayDialog("UnderwaterSystem", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        var depthSettings = AssetDatabase.LoadAssetAtPath<DepthSettings>(DepthSettingsPath);
        var profile = AssetDatabase.LoadAssetAtPath<UnderwaterProfile>(ProfilePath);
        var toneProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ToneProfilePath);
        if (depthSettings == null || profile == null || toneProfile == null)
        {
            EditorUtility.DisplayDialog("UnderwaterSystem", "설정 에셋이 없습니다. 메뉴 2(카메라 톤)와 4(수심대)를 먼저 실행해 주세요.", "확인");
            return;
        }

        // 기존 낱개 오브젝트는 프리팹으로 대체. 지울 때 햇빛·주변광은 원래 값으로 돌아간다
        foreach (var name in new[] { "DepthZoneSystem", "Volume_ThirdPerson", "UnderwaterSystem" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        var root = new GameObject("UnderwaterSystem");
        var sys = root.AddComponent<DepthZoneSystem>();
        sys.depthSettings = depthSettings;
        sys.profile = profile;
        // 씬 오브젝트는 프리팹에 넣을 수 없어서 비워 둔다 (대상은 메인 카메라, 태양은 자동 탐색)
        sys.target = null;
        sys.sun = null;

        var volumeGO = new GameObject("Volume_ThirdPerson");
        volumeGO.transform.SetParent(root.transform, false);
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;   // 팀원 씬의 기본 Global Volume(보통 0)보다 우선
        volume.weight = 1f;
        volume.sharedProfile = toneProfile;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Light");
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

        // LightScene에서만 잠수정 대용을 수심 기준으로 쓴다 (프리팹 원본은 그대로)
        var sub = GameObject.Find("Submarine_Proxy");
        if (sub != null)
        {
            sys.target = sub.transform;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sys);
        }

        EditorSceneManager.MarkSceneDirty(lightScene);
        EditorSceneManager.SaveScene(lightScene);

        // MainScene은 통합 작업이 들어가므로 이미 있으면 건드리지 않는다
        bool createdMain = false;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
        {
            CreateMainScene(prefab, depthSettings, profile);
            createdMain = true;
            EditorSceneManager.OpenScene(LightScenePath, OpenSceneMode.Single);
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[UnderwaterSystem] {PrefabPath} 생성, LightScene에 배치" + (createdMain ? $", {MainScenePath} 생성" : $" ({MainScenePath}는 이미 있어 그대로 둠)"));
    }

    // 통합 씬 뼈대: 태양, 임시 카메라, UnderwaterSystem, 수심 안내선, 파트별 자리
    static void CreateMainScene(GameObject prefab, DepthSettings depthSettings, UnderwaterProfile profile)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = sun;

        // 잠수정 프리팹(조작·UI)이 들어오면 그 카메라로 바꾼다
        var camGO = new GameObject("Main Camera (임시)");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, -10f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        camGO.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        PrefabUtility.InstantiatePrefab(prefab, scene);

        var guidesGO = new GameObject("DepthGuides");
        var guides = guidesGO.AddComponent<DepthGuides>();
        guides.depthSettings = depthSettings;
        guides.profile = profile;

        foreach (var group in new[] { "== 해저 환경 ==", "== 물·생물 ==", "== 조작·UI ==" })
            new GameObject(group);

        EditorSceneManager.SaveScene(scene, MainScenePath);
    }
}
