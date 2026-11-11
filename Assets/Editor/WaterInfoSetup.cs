using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// DepthGuides가 쓰는 DepthSettings/UnderwaterProfile을 그대로 재사용해 WaterInfoProvider를 배치
public class WaterInfoSetup
{
    [MenuItem("Tools/수심별 바닷물 정보 제공자 설정")]
    public static void Setup()
    {
        DepthGuides guides = Object.FindFirstObjectByType<DepthGuides>();
        if (guides == null || guides.depthSettings == null || guides.profile == null)
        {
            EditorUtility.DisplayDialog("설정 실패", "씬에서 DepthGuides(depthSettings/profile 연결된 상태)를 찾을 수 없습니다.", "확인");
            return;
        }

        WaterInfoProvider provider = Object.FindFirstObjectByType<WaterInfoProvider>();
        if (provider == null)
        {
            GameObject go = new GameObject("WaterInfoProvider");
            provider = go.AddComponent<WaterInfoProvider>();
        }

        provider.depthSettings = guides.depthSettings;
        provider.profile = guides.profile;

        EditorUtility.SetDirty(provider);
        Debug.Log("[바닷물 정보] WaterInfoProvider 설정 완료.");
        Selection.activeGameObject = provider.gameObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
