using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Searchlight 프리팹을 만들고 LightScene의 잠수정 대용 앞에 단다. 메뉴: Light > 6
public static class SearchlightBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";
    const string PrefabFolder = "Assets/Prefabs/Light";
    const string PrefabPath = PrefabFolder + "/Searchlight.prefab";

    // 시작값: 바깥 50°, 안쪽 30°, 사거리 25m
    const float SpotAngle = 50f;
    const float InnerAngle = 30f;
    const float Range = 25f;
    const float Intensity = 30f;
    static readonly Color LampColor = new Color(1f, 0.96f, 0.9f);   // 살짝 따뜻한 흰색

    [MenuItem("Light/6. 탐조등 (Searchlight)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("탐조등", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("탐조등", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        var sub = GameObject.Find("Submarine_Proxy");
        if (sub == null)
        {
            EditorUtility.DisplayDialog("탐조등", "LightScene에 Submarine_Proxy가 없습니다.", "확인");
            return;
        }

        foreach (var name in new[] { "Searchlight", "SearchlightTestInput" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        // 루트는 잠수정 앞쪽 중앙, 등 두 개를 좌우로 벌려 단다. 실제 ROV처럼 약간 아래를 비춤
        var root = new GameObject("Searchlight");
        root.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        CreateLamp(root.transform, "Lamp_L", -0.4f);
        CreateLamp(root.transform, "Lamp_R", 0.4f);
        var controller = root.AddComponent<SearchlightController>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Light");
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

        // 잠수정 대용 몸체(길이 2.5m)의 앞 끝에 붙인다
        root.transform.SetParent(sub.transform, false);
        root.transform.localPosition = new Vector3(0f, 0f, 1.3f);

        // 테스트용 키 (L). 잠수정이 오면 조작 파트 입력으로 바뀐다
        var testGO = new GameObject("SearchlightTestInput");
        var test = testGO.AddComponent<SearchlightTestInput>();
        test.searchlight = controller;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[탐조등] {PrefabPath} 생성, Submarine_Proxy 앞에 장착 (Play 중 L 키로 켜고 끄기)");
    }

    // 탐조등 확인용 무대. 잠수정 자식이라 같이 내려간다. 다 보면 체크를 꺼 두면 된다
    [MenuItem("Light/6-1. 탐조등 확인 무대 (바닥·벽)")]
    public static void BuildTestStage()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = EditorSceneManager.GetActiveScene();
        var sub = GameObject.Find("Submarine_Proxy");
        if (scene.path != ScenePath || sub == null)
        {
            EditorUtility.DisplayDialog("탐조등", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Light/Test/Test_Floor.mat");
        var whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Light/Test/Test_White.mat");

        var old = sub.transform.Find("SearchlightTestStage");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var stage = new GameObject("SearchlightTestStage").transform;
        stage.SetParent(sub.transform, false);

        // 바닥: 잠수정 4m 아래, 앞쪽으로 넓게
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "StageFloor";
        floor.transform.SetParent(stage, false);
        floor.transform.localPosition = new Vector3(0f, -4f, 15f);
        floor.transform.localScale = new Vector3(4f, 1f, 4f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // 벽: 18m 앞
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "StageWall";
        wall.transform.SetParent(stage, false);
        wall.transform.localPosition = new Vector3(0f, 2f, 18f);
        wall.transform.localScale = new Vector3(30f, 12f, 0.5f);
        wall.GetComponent<Renderer>().sharedMaterial = whiteMat;

        // 기둥: 원뿔 안 6/10/14m
        float[] dist = { 6f, 10f, 14f };
        float[] side = { -1.5f, 1.5f, 0f };
        for (int i = 0; i < dist.Length; i++)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"StagePillar_{dist[i]:0}m";
            pillar.transform.SetParent(stage, false);
            pillar.transform.localPosition = new Vector3(side[i], -2f, dist[i]);
            pillar.transform.localScale = new Vector3(0.6f, 4f, 0.6f);
            pillar.GetComponent<Renderer>().sharedMaterial = whiteMat;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = stage.gameObject;
        Debug.Log("[탐조등] 확인 무대 생성. Submarine_Proxy Y를 -300으로 내리고 Searchlight의 Is On을 껐다 켜 보세요");
    }

    static void CreateLamp(Transform parent, string name, float x)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = SpotAngle;
        light.innerSpotAngle = InnerAngle;
        light.range = Range;
        light.intensity = Intensity;
        light.color = LampColor;
        light.shadows = LightShadows.None;   // 그림자는 성능 측정 후 결정
    }
}
