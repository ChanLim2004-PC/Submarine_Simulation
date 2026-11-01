using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 조명 판정 확인용 표시기를 LightScene 잠수정 앞에 놓는다. 메뉴: Light > 7
public static class LightingQueryTestBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";
    const string GroupName = "ExposureChecks";

    [MenuItem("Light/7. 조명 판정 확인 (LightingQuery)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("조명 판정", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        var sub = GameObject.Find("Submarine_Proxy");
        if (scene.path != ScenePath || sub == null)
        {
            EditorUtility.DisplayDialog("조명 판정", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        var old = sub.transform.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 잠수정 기준 위치. 탐조등은 앞 끝(z 1.3)에서 8° 아래를 비춘다
        var group = new GameObject(GroupName).transform;
        group.SetParent(sub.transform, false);
        AddProbe(group, "원뿔 가운데", new Vector3(0f, -1.2f, 9f));
        AddProbe(group, "원뿔 가장자리", new Vector3(3.5f, -1.2f, 9f));
        AddProbe(group, "원뿔 밖", new Vector3(8f, 0f, 6f));
        AddProbe(group, "사거리 밖", new Vector3(0f, -3.5f, 28f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = group.gameObject;
        Debug.Log("[조명 판정] 확인용 표시기 4개 배치. 잠수정을 200m보다 깊이 내려야 햇빛 영향 없이 탐조등만 볼 수 있습니다");
    }

    static void AddProbe(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<LightExposureProbe>();
    }
}
