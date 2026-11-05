using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// 바위 윗면에 모래/퇴적물이 살짝 쌓인 느낌을 표현 - 커스텀 셰이더 대신 작은 모래 덩어리 메쉬를 얹는 방식
public class RockCrustGenerator
{
    const int LayoutSeed = 29;
    const float CrustChance = 0.3f;

    [MenuItem("Tools/해저 바위 모래 크러스트 배치")]
    public static void ScatterCrust()
    {
        GameObject propsContainer = GameObject.Find("SeabedProps");
        if (propsContainer == null)
        {
            EditorUtility.DisplayDialog("모래 크러스트 배치 실패", "먼저 Tools > 해저 프롭 배치 (Poisson Disk Sampling) 으로 바위를 배치해주세요.", "확인");
            return;
        }

        GameObject oldContainer = GameObject.Find("SeabedRockCrust");
        if (oldContainer != null) Object.DestroyImmediate(oldContainer);
        GameObject container = new GameObject("SeabedRockCrust");

        System.Random rng = new System.Random(LayoutSeed);
        Material crustMat = CreateCrustMaterial();

        var targets = new List<Transform>();
        foreach (Transform t in propsContainer.transform)
        {
            if (t.name == "Rock" || t.name == "Rock_Landmark") targets.Add(t);
        }

        int placed = 0;
        foreach (Transform rockT in targets)
        {
            if (rng.NextDouble() >= CrustChance) continue;

            var renderers = rockT.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            int blobCount = rockT.name == "Rock_Landmark" ? rng.Next(2, 4) : 1;
            for (int i = 0; i < blobCount; i++)
            {
                GameObject blob = CreateCrustBlob(crustMat);
                blob.transform.SetParent(container.transform, true);

                float ox = Lerp01(rng, -0.35f, 0.35f) * bounds.size.x;
                float oz = Lerp01(rng, -0.35f, 0.35f) * bounds.size.z;
                float topY = bounds.max.y - bounds.size.y * Lerp01(rng, 0f, 0.25f);
                blob.transform.position = new Vector3(bounds.center.x + ox, topY, bounds.center.z + oz);
                blob.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                float scale = Lerp01(rng, 0.18f, 0.4f) * Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                blob.transform.localScale = new Vector3(scale, scale * 0.25f, scale);
            }
            placed++;
        }

        Debug.Log($"[모래 크러스트] 바위 {placed}개에 적용 완료.");
        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static Material CreateCrustMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.72f, 0.62f, 0.46f));
        mat.SetFloat("_Smoothness", 0.08f);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    static GameObject CreateCrustBlob(Material mat)
    {
        GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blob.name = "SandCrust";
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return blob;
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
