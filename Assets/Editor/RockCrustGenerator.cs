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

            // 레이캐스트로 실제 바위 표면 위치를 찾기 위해 임시 콜라이더를 붙였다가 끝나면 제거
            var tempColliders = new List<Collider>();
            foreach (var mf in rockT.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    tempColliders.Add(mf.gameObject.AddComponent<MeshCollider>());
            }

            int blobCount = rockT.name == "Rock_Landmark" ? rng.Next(2, 4) : 1;
            int blobsMade = 0;
            for (int i = 0; i < blobCount; i++)
            {
                float ox = Lerp01(rng, -0.3f, 0.3f) * bounds.size.x;
                float oz = Lerp01(rng, -0.3f, 0.3f) * bounds.size.z;
                Vector3 rayStart = new Vector3(bounds.center.x + ox, bounds.max.y + 1f, bounds.center.z + oz);

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, bounds.size.y + 2f)
                    && hit.collider.transform.IsChildOf(rockT))
                {
                    GameObject blob = CreateCrustBlob(crustMat, rng);
                    blob.transform.SetParent(container.transform, true);
                    blob.transform.position = hit.point + hit.normal * 0.03f;
                    blob.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    blobsMade++;
                }
            }

            foreach (var c in tempColliders) Object.DestroyImmediate(c);
            if (blobsMade > 0) placed++;
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

    // 바위 크기에 비례시키지 않고 작은 고정 범위로 제한 (큰 바위 군집에서 거대한 덩어리가 생기는 것을 방지)
    static GameObject CreateCrustBlob(Material mat, System.Random rng)
    {
        GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blob.name = "SandCrust";
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.GetComponent<MeshRenderer>().sharedMaterial = mat;

        float scale = Lerp01(rng, 0.25f, 0.6f);
        blob.transform.localScale = new Vector3(scale, scale * 0.3f, scale);
        return blob;
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
