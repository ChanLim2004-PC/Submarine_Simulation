using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SeabedCullerSetup
{
    [MenuItem("Tools/해저 소품 거리 컬링 설정")]
    public static void Setup()
    {
        SeabedDistanceCuller culler = Object.FindFirstObjectByType<SeabedDistanceCuller>();
        if (culler == null)
        {
            GameObject go = new GameObject("SeabedDistanceCuller");
            culler = go.AddComponent<SeabedDistanceCuller>();
        }

        EditorUtility.SetDirty(culler);
        Debug.Log("[거리 컬링] SeabedDistanceCuller 설정 완료.");
        Selection.activeGameObject = culler.gameObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
