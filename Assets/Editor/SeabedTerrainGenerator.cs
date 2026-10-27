using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 해저 지형 블록아웃 자동 생성 (해저산 / 협곡 / 평원)
// 실행: Unity 메뉴 Tools -> 해저 지형 생성 (Block-out)
public class SeabedTerrainGenerator
{
    [MenuItem("Tools/해저 지형 생성 (Block-out)")]
    public static void GenerateSeabedTerrain()
    {
        int resolution = 513; // Unity heightmap 규칙: 2^n + 1
        float terrainWidth = 500f;
        float terrainLength = 500f;
        float terrainHeight = 60f;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float nx = (float)x / resolution;
                float nz = (float)z / resolution;

                // 평원 (base) + 약한 노이즈
                float h = 0.35f + Mathf.PerlinNoise(nx * 8f, nz * 8f) * 0.03f;

                // 해저산 (seamount)
                float sx = nx - 0.25f;
                float sz = nz - 0.3f;
                float seamountDist2 = sx * sx + sz * sz;
                h += 0.35f * Mathf.Exp(-seamountDist2 / 0.02f);

                // 협곡 (trench)
                float tx = nx - 0.7f;
                float tz = nz - 0.65f;
                float trenchDist2 = tx * tx + tz * tz;
                h -= 0.30f * Mathf.Exp(-trenchDist2 / 0.015f);

                heights[z, x] = Mathf.Clamp01(h);
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // TerrainData를 실제 프로젝트 에셋 파일로 저장.
        // 이걸 안 하면 메모리에만 존재하게 돼서 스크립트 재컴파일/에디터 재시작 시 지형이 사라짐.
        string folder = "Assets/Terrain";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Terrain");

        string assetPath = $"{folder}/SeabedTerrainData.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(terrainData, assetPath);
        AssetDatabase.SaveAssets();

        // 기존에 만들어둔 SeabedTerrain 있으면 지우고 새로 생성
        GameObject old = GameObject.Find("SeabedTerrain");
        if (old != null)
            Object.DestroyImmediate(old);

        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.name = "SeabedTerrain";
        terrainObj.transform.position = new Vector3(-terrainWidth / 2f, -terrainHeight * 0.6f, -terrainLength / 2f);

        Selection.activeGameObject = terrainObj;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
