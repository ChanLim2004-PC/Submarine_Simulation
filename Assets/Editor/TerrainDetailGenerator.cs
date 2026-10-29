using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 바닥 잔돌 채우기: Terrain Detail(GPU 인스턴싱)로 지형 전체에 밀도 있게 깔아줌
// 개별 GameObject가 아니라 Terrain 자체가 그려주는 방식이라 수천~수만 개도 가벼움
public class TerrainDetailGenerator
{
    const string PrototypeMeshPath = "Assets/StylizedRocksPackFREE/Meshes/SM_RockPack_Rock_04.fbx";
    const string PrototypePrefabPath = "Assets/Terrain/DetailRockPrototype.prefab";
    const string TexBC = "Assets/StylizedRocksPackFREE/Textures/Rocks/T_RockPack_Rocks_01_BC.tga";
    const string TexN = "Assets/StylizedRocksPackFREE/Textures/Rocks/T_RockPack_Rocks_01_N.tga";

    [MenuItem("Tools/해저 바닥 디테일 채우기 (잔돌)")]
    public static void FillGroundDetail()
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/SeabedTerrainData.asset");
        GameObject terrainObj = GameObject.Find("SeabedTerrain");
        if (terrainData == null || terrainObj == null)
        {
            EditorUtility.DisplayDialog("디테일 채우기 실패", "먼저 해저 지형을 생성해주세요.", "확인");
            return;
        }
        Terrain terrain = terrainObj.GetComponent<Terrain>();

        GameObject prototypePrefab = BuildDetailPrototypePrefab();

        DetailPrototype detail = new DetailPrototype
        {
            prototype = prototypePrefab,
            renderMode = DetailRenderMode.VertexLit,
            usePrototypeMesh = true,
            useInstancing = true,
            minWidth = 0.3f,
            maxWidth = 0.9f,
            minHeight = 0.3f,
            maxHeight = 0.9f,
            healthyColor = Color.white,
            dryColor = Color.white,
            noiseSeed = 17
        };
        terrainData.detailPrototypes = new DetailPrototype[] { detail };

        int resolution = 1024;
        terrainData.SetDetailResolution(resolution, 32);

        int[,] map = new int[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;
                float ruggedness = RuggednessAt(terrainData, u, v);
                map[y, x] = ruggedness < 0.08f ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 16f, Mathf.InverseLerp(0.08f, 0.8f, ruggedness)));
            }
        }
        terrainData.SetDetailLayer(0, 0, 0, map);

        terrain.detailObjectDistance = 350f;

        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[바닥 디테일] 잔돌 디테일 레이어 적용 완료.");
    }

    static float RuggednessAt(TerrainData terrainData, float u, float v)
    {
        float steepness01 = Mathf.Clamp01(terrainData.GetSteepness(u, v) / 60f);
        float height01 = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
        return Mathf.Clamp01(steepness01 * 0.7f + Mathf.InverseLerp(0.35f, 0.7f, height01) * 0.5f);
    }

    static GameObject BuildDetailPrototypePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePrefabPath);
        if (existing != null) return existing;

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PrototypeMeshPath);
        Texture2D bc = AssetDatabase.LoadAssetAtPath<Texture2D>(TexBC);
        Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexN);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (bc != null) mat.SetTexture("_BaseMap", bc);
        if (n != null) mat.SetTexture("_BumpMap", n);
        mat.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(mat, "Assets/Terrain/DetailRockMaterial.mat");

        GameObject temp = new GameObject("DetailRockPrototype");
        var mf = temp.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = temp.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrototypePrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }
}
