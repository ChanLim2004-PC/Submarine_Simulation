using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 해저에 녹슨 드럼통/나무상자 잔해/그물 더미를 드문드문 배치 (교수님 피드백: 해저 쓰레기 에셋)
public class DebrisScatterGenerator
{
    const int LayoutSeed = 41;
    const int DebrisCount = 24;

    [MenuItem("Tools/해저 쓰레기 디테일 배치")]
    public static void ScatterDebris()
    {
        GameObject terrainObj = GameObject.Find("SeabedTerrain");
        if (terrainObj == null)
        {
            EditorUtility.DisplayDialog("쓰레기 배치 실패", "먼저 Tools > 해저 지형 생성 (Block-out) 으로 지형을 만들어주세요.", "확인");
            return;
        }
        Terrain terrain = terrainObj.GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainOrigin = terrainObj.transform.position;
        float width = terrainData.size.x;
        float length = terrainData.size.z;

        GameObject oldContainer = GameObject.Find("SeabedDebris");
        if (oldContainer != null) Object.DestroyImmediate(oldContainer);
        GameObject container = new GameObject("SeabedDebris");

        System.Random rng = new System.Random(LayoutSeed);
        Material drumMat = CreateMaterial(new Color(0.35f, 0.18f, 0.08f), 0.25f, 0.6f);
        Material crateMat = CreateMaterial(new Color(0.28f, 0.2f, 0.12f), 0.1f, 0f);
        Material netMat = CreateMaterial(new Color(0.22f, 0.26f, 0.24f), 0.15f, 0f);

        for (int i = 0; i < DebrisCount; i++)
        {
            float x = Lerp01(rng, 0.05f, 0.95f) * width;
            float z = Lerp01(rng, 0.05f, 0.95f) * length;
            float u = x / width;
            float v = z / length;
            float worldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(u, v);
            Vector3 pos = new Vector3(terrainOrigin.x + x, worldY, terrainOrigin.z + z);

            int kind = rng.Next(3);
            GameObject debris = kind == 0 ? CreateDrum(drumMat, rng)
                               : kind == 1 ? CreateCrateCluster(crateMat, rng)
                               : CreateNetPile(netMat, rng);
            debris.transform.SetParent(container.transform, true);
            debris.transform.position = pos;
            debris.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        }

        Debug.Log($"[쓰레기 배치] {DebrisCount}개 배치 완료.");
        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static GameObject CreateDrum(Material mat, System.Random rng)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "RustyDrum";
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        float h = Lerp01(rng, 0.7f, 1.1f);
        float r = Lerp01(rng, 0.35f, 0.5f);
        go.transform.localScale = new Vector3(r, h, r);
        // 쓰러져 있는 드럼통도 섞어서 변화를 줌
        if (rng.NextDouble() < 0.4) go.transform.Rotate(90f, 0f, 0f);
        return go;
    }

    static GameObject CreateCrateCluster(Material mat, System.Random rng)
    {
        GameObject root = new GameObject("CrateDebris");
        int count = rng.Next(2, 4);
        for (int i = 0; i < count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "CrateFragment";
            cube.transform.SetParent(root.transform, false);
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            float s = Lerp01(rng, 0.4f, 0.7f);
            cube.transform.localScale = new Vector3(s, s * Lerp01(rng, 0.5f, 1f), s);
            cube.transform.localPosition = new Vector3(Lerp01(rng, -0.3f, 0.3f), 0f, Lerp01(rng, -0.3f, 0.3f));
            cube.transform.localRotation = Quaternion.Euler(Lerp01(rng, -20f, 20f), (float)rng.NextDouble() * 360f, Lerp01(rng, -20f, 20f));
        }
        return root;
    }

    static GameObject CreateNetPile(Material mat, System.Random rng)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "NetPile";
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        float wide = Lerp01(rng, 0.9f, 1.6f);
        float flat = Lerp01(rng, 0.12f, 0.22f);
        go.transform.localScale = new Vector3(wide, flat, wide * Lerp01(rng, 0.8f, 1.2f));
        return go;
    }

    static Material CreateMaterial(Color color, float smoothness, float metallic)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        return mat;
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
