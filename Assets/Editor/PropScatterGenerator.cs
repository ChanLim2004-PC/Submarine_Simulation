using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// 해저 프롭 배치: 지형 경사/높이 기반 가변 반경 Poisson Disk Sampling
public class PropScatterGenerator
{
    const int LayoutSeed = 11;
    const float minRadius = 18f;
    const float maxRadius = 70f;

    [MenuItem("Tools/해저 프롭 배치 (Poisson Disk Sampling)")]
    public static void ScatterProps()
    {
        string terrainAssetPath = "Assets/Terrain/SeabedTerrainData.asset";
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainAssetPath);
        if (terrainData == null)
        {
            EditorUtility.DisplayDialog("프롭 배치 실패", "먼저 Tools > 해저 지형 생성 (Block-out) 으로 지형을 만들어주세요.", "확인");
            return;
        }

        GameObject terrainObj = GameObject.Find("SeabedTerrain");
        if (terrainObj == null)
        {
            EditorUtility.DisplayDialog("프롭 배치 실패", "씬에서 SeabedTerrain 오브젝트를 찾을 수 없습니다.", "확인");
            return;
        }

        GameObject oldContainer = GameObject.Find("SeabedProps");
        if (oldContainer != null)
            Object.DestroyImmediate(oldContainer);

        GameObject container = new GameObject("SeabedProps");

        float width = terrainData.size.x;
        float length = terrainData.size.z;
        Vector3 terrainOrigin = terrainObj.transform.position;

        float RuggednessAt(float u, float v)
        {
            float steepness01 = Mathf.Clamp01(terrainData.GetSteepness(u, v) / 60f);
            float height01 = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
            return Mathf.Clamp01(steepness01 * 0.7f + Mathf.InverseLerp(0.35f, 0.7f, height01) * 0.5f);
        }

        float RadiusAt(float u, float v) => Mathf.Lerp(maxRadius, minRadius, RuggednessAt(u, v));

        List<Vector2> points = PoissonDiskSample(width, length, RadiusAt, LayoutSeed);

        System.Random rng = new System.Random(LayoutSeed + 1);
        int placed = 0;
        foreach (Vector2 p in points)
        {
            float u = p.x / width;
            float v = p.y / length;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            float worldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(u, v);
            Vector3 worldPos = new Vector3(terrainOrigin.x + p.x, worldY, terrainOrigin.z + p.y);
            Vector3 normal = terrainData.GetInterpolatedNormal(u, v);

            float ruggedness = RuggednessAt(u, v);
            bool isRock = rng.NextDouble() < Mathf.Lerp(0.15f, 0.9f, ruggedness);
            GameObject prop = isRock ? CreateRockPlaceholder(rng) : CreateCoralPlaceholder(rng);
            prop.transform.SetParent(container.transform, true);
            prop.transform.position = worldPos;
            prop.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            placed++;
        }

        Debug.Log($"[프롭 배치] placeholder {placed}개 배치 완료.");

        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    const string RockPackPrefabFolder = "Assets/StylizedRocksPackFREE/Prefabs/Rocks";
    const string RockPackTextureFolder = "Assets/StylizedRocksPackFREE/Textures/Rocks";

    static GameObject[] rockPrefabsCache;
    static System.Collections.Generic.Dictionary<string, Material> rockMatCache;

    static GameObject[] LoadRockPrefabs()
    {
        if (rockPrefabsCache != null) return rockPrefabsCache;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RockPackPrefabFolder });
        rockPrefabsCache = new GameObject[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            rockPrefabsCache[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return rockPrefabsCache;
    }

    // 프리팹 이름에서 텍스처 세트 이름 추출 (예: PF_RockPack_Cliff_07_A -> Cliff_07, 없으면 Rocks_01)
    static string RockTextureSetFor(string prefabName)
    {
        if (prefabName.Contains("Cliff_07")) return "Cliff_07";
        if (prefabName.Contains("RockGroups_01")) return "RockGroups_01";
        if (prefabName.Contains("RockGroups_02")) return "RockGroups_02";
        return "Rocks_01";
    }

    // MM_CliffsMoss 셰이더가 데모팩에서 깨져있어서(서브그래프 누락), 같은 텍스처로 URP/Lit 머티리얼을 새로 만들어 대신 씀
    static Material RockMaterialFor(string textureSet)
    {
        rockMatCache ??= new System.Collections.Generic.Dictionary<string, Material>();
        if (rockMatCache.TryGetValue(textureSet, out var cached)) return cached;

        Texture2D bc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RockPackTextureFolder}/T_RockPack_{textureSet}_BC.tga");
        Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RockPackTextureFolder}/T_RockPack_{textureSet}_N.tga");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (bc != null) mat.SetTexture("_BaseMap", bc);
        if (n != null) mat.SetTexture("_BumpMap", n);
        mat.SetFloat("_Smoothness", 0.25f);

        rockMatCache[textureSet] = mat;
        return mat;
    }

    static GameObject CreateRockPlaceholder(System.Random rng)
    {
        GameObject[] prefabs = LoadRockPrefabs();
        GameObject prefab = prefabs[rng.Next(prefabs.Length)];
        GameObject rock = Object.Instantiate(prefab);
        rock.name = "Rock";

        Material mat = RockMaterialFor(RockTextureSetFor(prefab.name));
        foreach (var renderer in rock.GetComponentsInChildren<MeshRenderer>())
            renderer.sharedMaterial = mat;

        float scale = Lerp01(rng, 0.6f, 1.8f);
        rock.transform.localScale = Vector3.one * scale;
        rock.transform.Rotate(Lerp01(rng, -10f, 10f), 0f, Lerp01(rng, -10f, 10f));

        var info = rock.AddComponent<OceanPropInfo>();
        info.category = "Rock";
        info.displayName = "바위";

        return rock;
    }

    static GameObject CreateCoralPlaceholder(System.Random rng)
    {
        GameObject root = new GameObject("Coral");
        int blobCount = rng.Next(2, 4);
        Color tint = Color.HSVToRGB(Lerp01(rng, 0.0f, 0.1f) + (rng.Next(2) == 0 ? 0f : 0.75f), 0.55f, 0.8f);

        for (int i = 0; i < blobCount; i++)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "CoralBlob";
            blob.transform.SetParent(root.transform, false);
            float scale = Lerp01(rng, 0.8f, 2.2f);
            blob.transform.localScale = Vector3.one * scale;
            blob.transform.localPosition = new Vector3(Lerp01(rng, -1f, 1f), scale * 0.4f, Lerp01(rng, -1f, 1f));

            var renderer = blob.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = tint;
            renderer.sharedMaterial = mat;
        }

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "Coral";
        info.displayName = "산호";

        return root;
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // Bridson 알고리즘 변형, 가변 반경
    static List<Vector2> PoissonDiskSample(float width, float height, System.Func<float, float, float> radiusAt, int seed)
    {
        var rng = new System.Random(seed);
        float cellSize = minRadius / Mathf.Sqrt(2f);
        int gridW = Mathf.CeilToInt(width / cellSize);
        int gridH = Mathf.CeilToInt(height / cellSize);
        int[,] grid = new int[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                grid[x, y] = -1;

        List<Vector2> points = new List<Vector2>();
        List<float> radii = new List<float>();
        List<int> active = new List<int>();

        Vector2 first = new Vector2((float)rng.NextDouble() * width, (float)rng.NextDouble() * height);
        AddPoint(first, radiusAt(first.x / width, first.y / height), points, radii, active, grid, cellSize, gridW, gridH);

        const int k = 24;
        while (active.Count > 0)
        {
            int listIdx = rng.Next(active.Count);
            int idx = active[listIdx];
            Vector2 center = points[idx];
            float centerRadius = radii[idx];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = centerRadius * (1f + (float)rng.NextDouble());
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                if (candidate.x < 0f || candidate.x >= width || candidate.y < 0f || candidate.y >= height)
                    continue;

                float candRadius = radiusAt(candidate.x / width, candidate.y / height);
                if (IsFarEnough(candidate, candRadius, points, radii, grid, cellSize, gridW, gridH))
                {
                    AddPoint(candidate, candRadius, points, radii, active, grid, cellSize, gridW, gridH);
                    found = true;
                }
            }

            if (!found)
                active.RemoveAt(listIdx);
        }

        return points;
    }

    static void AddPoint(Vector2 p, float r, List<Vector2> points, List<float> radii, List<int> active, int[,] grid, float cellSize, int gridW, int gridH)
    {
        points.Add(p);
        radii.Add(r);
        active.Add(points.Count - 1);
        int gx = Mathf.Clamp((int)(p.x / cellSize), 0, gridW - 1);
        int gy = Mathf.Clamp((int)(p.y / cellSize), 0, gridH - 1);
        grid[gx, gy] = points.Count - 1;
    }

    static bool IsFarEnough(Vector2 candidate, float candRadius, List<Vector2> points, List<float> radii, int[,] grid, float cellSize, int gridW, int gridH)
    {
        int gx = Mathf.Clamp((int)(candidate.x / cellSize), 0, gridW - 1);
        int gy = Mathf.Clamp((int)(candidate.y / cellSize), 0, gridH - 1);

        int searchRadius = Mathf.CeilToInt((candRadius + cellSize) / cellSize);
        for (int x = gx - searchRadius; x <= gx + searchRadius; x++)
        {
            if (x < 0 || x >= gridW) continue;
            for (int y = gy - searchRadius; y <= gy + searchRadius; y++)
            {
                if (y < 0 || y >= gridH) continue;
                int pi = grid[x, y];
                if (pi < 0) continue;

                float required = Mathf.Max(candRadius, radii[pi]);
                if (Vector2.Distance(candidate, points[pi]) < required)
                    return false;
            }
        }
        return true;
    }
}
