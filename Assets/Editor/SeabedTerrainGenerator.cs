using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// 해저 지형 블록아웃 자동 생성 (해저산 / 협곡 / 평원) + 모래/암석 텍스처 블렌딩
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
        terrainData.alphamapResolution = 512;
        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float nx = (float)x / resolution;
                float nz = (float)z / resolution;

                // 평원 (base): 다중 옥타브 노이즈로 자연스러운 굴곡
                float h = 0.35f + Fbm(nx * 6f, nz * 6f, 4) * 0.06f;

                // 해저산 (seamount): 중심을 약간 왜곡시켜 완전한 원형을 피함
                float warpX = (Fbm(nx * 3f, nz * 3f, 3) - 0.5f) * 0.08f;
                float warpZ = (Fbm(nx * 3f + 5f, nz * 3f + 5f, 3) - 0.5f) * 0.08f;
                float sx = nx - 0.25f + warpX;
                float sz = nz - 0.3f + warpZ;
                float seamountDist2 = sx * sx + sz * sz;
                h += 0.35f * Mathf.Exp(-seamountDist2 / 0.02f);

                // 협곡 (trench)
                float tx = nx - 0.7f + warpX;
                float tz = nz - 0.65f + warpZ;
                float trenchDist2 = tx * tx + tz * tz;
                h -= 0.30f * Mathf.Exp(-trenchDist2 / 0.015f);

                // 잔잔한 고주파 디테일 (암반 질감)
                h += (Fbm(nx * 40f, nz * 40f, 2) - 0.5f) * 0.01f;

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

        PaintTerrainLayers(terrainData, folder);

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

    // 다중 옥타브 Perlin 노이즈 (0~1)
    static float Fbm(float x, float y, int octaves)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;
        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.2f;
        }
        return total / maxValue;
    }

    // 높이/경사 기반으로 모래(평원)와 암석(해저산 봉우리·협곡 절벽) 텍스처를 블렌딩
    static void PaintTerrainLayers(TerrainData terrainData, string baseFolder)
    {
        string texFolder = $"{baseFolder}/Textures";
        if (!AssetDatabase.IsValidFolder(texFolder))
            AssetDatabase.CreateFolder(baseFolder, "Textures");

        TerrainLayer sandLayer = CreateOrUpdateLayer(texFolder, "SandLayer",
            new Color(0.76f, 0.70f, 0.52f), new Color(0.68f, 0.62f, 0.45f));
        TerrainLayer rockLayer = CreateOrUpdateLayer(texFolder, "RockLayer",
            new Color(0.33f, 0.32f, 0.30f), new Color(0.22f, 0.21f, 0.20f));

        terrainData.terrainLayers = new TerrainLayer[] { sandLayer, rockLayer };

        int alphaRes = terrainData.alphamapResolution;
        float[,,] alphamap = new float[alphaRes, alphaRes, 2];

        for (int y = 0; y < alphaRes; y++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float normX = (float)x / (alphaRes - 1);
                float normY = (float)y / (alphaRes - 1);
                float steepness = terrainData.GetSteepness(normX, normY) / 90f; // 0~1
                float height01 = terrainData.GetInterpolatedHeight(normX, normY) / terrainData.size.y;

                // 경사가 급하거나(절벽) 높이가 높을수록(해저산 봉우리) 암석 비율 증가
                float rockWeight = Mathf.Clamp01(steepness * 1.6f + Mathf.InverseLerp(0.45f, 0.75f, height01) * 0.8f);
                rockWeight = Mathf.Clamp01(rockWeight + (Mathf.PerlinNoise(normX * 40f, normY * 40f) - 0.5f) * 0.15f);

                alphamap[y, x, 1] = rockWeight;
                alphamap[y, x, 0] = 1f - rockWeight;
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamap);
    }

    static TerrainLayer CreateOrUpdateLayer(string folder, string name, Color baseColor, Color variantColor)
    {
        string layerPath = $"{folder}/{name}.terrainlayer";
        string texPath = $"{folder}/{name}.png";

        Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, true);
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                tex.SetPixel(x, y, Color.Lerp(variantColor, baseColor, n));
            }
        }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);

        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);
        }
        layer.diffuseTexture = importedTex;
        layer.tileSize = new Vector2(30f, 30f);
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();

        return layer;
    }
}
