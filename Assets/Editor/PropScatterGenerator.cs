using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// 해저 프롭 배치: 지형 경사/높이 기반 가변 반경 Poisson Disk Sampling
public class PropScatterGenerator
{
    const int LayoutSeed = 11;
    const float minRadius = 16f;
    const float maxRadius = 100f;

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
            // 드물게 압도적으로 큰 랜드마크 바위를 섞어서 무더기 크기 자체에 편차를 줌 (레퍼런스처럼 큰 덩어리 주변에 중간·자잘한 돌)
            bool isLandmark = isRock && ruggedness > 0.45f && rng.NextDouble() < 0.12f;
            // 냉수성 산호는 바위·험지에 붙어 자라므로 산호 등장률도 ruggedness에 비례, 평지는 산호도 없이 맨바닥으로 남김
            bool isCoral = !isRock && rng.NextDouble() < Mathf.Lerp(0.05f, 0.55f, ruggedness);

            GameObject prop = isRock ? CreateRockPlaceholder(rng, ruggedness, isLandmark) : (isCoral ? CreateCoralPlaceholder(rng) : null);
            if (prop != null)
            {
                prop.transform.SetParent(container.transform, true);
                prop.transform.position = worldPos;
                prop.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                placed++;
            }

            // 험한 지점(해저산 사면·절벽)에는 큰 바위 주변에 작은 돌(자갈)을 흩뿌려 talus field처럼 무더기로 보이게 함
            // 랜드마크 바위는 영역을 넓게/densely 채워서 "큰 바위 + 중간 바위 + 자갈"의 크기 계단을 만듦
            if (isRock && ruggedness > 0.4f)
            {
                float haloRadius = isLandmark ? 40f : 22f;
                float minSpacing = isLandmark ? 7f : 4f;
                List<Vector2> haloOffsets = new List<Vector2>();

                int gravelCount = isLandmark ? rng.Next(7, 13) : rng.Next(2, 5);
                for (int g = 0; g < gravelCount; g++)
                {
                    Vector2 offset = PickSpacedOffset(rng, haloRadius, minSpacing, haloOffsets);
                    float gu = u + offset.x / width;
                    float gv = v + offset.y / length;
                    gu = Mathf.Clamp01(gu);
                    gv = Mathf.Clamp01(gv);

                    float gWorldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(gu, gv);
                    Vector3 gWorldPos = new Vector3(terrainOrigin.x + gu * width, gWorldY, terrainOrigin.z + gv * length);
                    Vector3 gNormal = terrainData.GetInterpolatedNormal(gu, gv);

                    GameObject gravel = CreateGravelPlaceholder(rng);
                    gravel.transform.SetParent(container.transform, true);
                    gravel.transform.position = gWorldPos;
                    gravel.transform.rotation = Quaternion.FromToRotation(Vector3.up, gNormal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }

                if (isLandmark)
                {
                    int mediumCount = rng.Next(2, 4);
                    for (int m = 0; m < mediumCount; m++)
                    {
                        Vector2 offset = PickSpacedOffset(rng, haloRadius * 0.6f, minSpacing * 1.4f, haloOffsets);
                        float mu = u + offset.x / width;
                        float mv = v + offset.y / length;
                        mu = Mathf.Clamp01(mu);
                        mv = Mathf.Clamp01(mv);

                        float mWorldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(mu, mv);
                        Vector3 mWorldPos = new Vector3(terrainOrigin.x + mu * width, mWorldY, terrainOrigin.z + mv * length);
                        Vector3 mNormal = terrainData.GetInterpolatedNormal(mu, mv);

                        GameObject medium = CreateRockPlaceholder(rng, ruggedness, false);
                        medium.transform.SetParent(container.transform, true);
                        medium.transform.position = mWorldPos;
                        medium.transform.rotation = Quaternion.FromToRotation(Vector3.up, mNormal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    }
                }
            }
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

    static GameObject[] cliffPrefabsCache, groupPrefabsCache, singlePrefabsCache;

    static void SplitRockPrefabsByTier()
    {
        if (cliffPrefabsCache != null) return;
        var cliffs = new List<GameObject>();
        var groups = new List<GameObject>();
        var singles = new List<GameObject>();
        foreach (var p in LoadRockPrefabs())
        {
            if (p.name.Contains("Cliff")) cliffs.Add(p);
            else if (p.name.Contains("RockGroups")) groups.Add(p);
            else singles.Add(p);
        }
        cliffPrefabsCache = cliffs.ToArray();
        groupPrefabsCache = groups.ToArray();
        singlePrefabsCache = singles.ToArray();
    }

    // 험할수록 RockGroups(뭉친 바위 더미)·Cliff(큰 랜드마크) 비중을 높이고, 완만할수록 낱개 작은 바위 위주로 감
    static GameObject PickRockPrefab(System.Random rng, float ruggedness)
    {
        SplitRockPrefabsByTier();
        double roll = rng.NextDouble();
        double cliffChance = ruggedness > 0.65f ? 0.25 : 0.0;
        double groupChance = Mathf.Lerp(0.1f, 0.75f, ruggedness);

        if (roll < cliffChance && cliffPrefabsCache.Length > 0)
            return cliffPrefabsCache[rng.Next(cliffPrefabsCache.Length)];
        if (roll < cliffChance + groupChance && groupPrefabsCache.Length > 0)
            return groupPrefabsCache[rng.Next(groupPrefabsCache.Length)];
        return singlePrefabsCache[rng.Next(singlePrefabsCache.Length)];
    }

    static GameObject InstantiateRock(GameObject prefab, string name, float scaleMin, float scaleMax, System.Random rng)
    {
        GameObject rock = Object.Instantiate(prefab);
        rock.name = name;

        Material mat = RockMaterialFor(RockTextureSetFor(prefab.name));
        foreach (var renderer in rock.GetComponentsInChildren<MeshRenderer>())
            renderer.sharedMaterial = mat;

        float scale = Lerp01(rng, scaleMin, scaleMax);
        rock.transform.localScale = Vector3.one * scale;
        rock.transform.Rotate(Lerp01(rng, -10f, 10f), 0f, Lerp01(rng, -10f, 10f));

        var info = rock.AddComponent<OceanPropInfo>();
        info.category = "Rock";
        info.displayName = "바위";

        return rock;
    }

    static GameObject CreateRockPlaceholder(System.Random rng, float ruggedness, bool isLandmark = false)
    {
        if (isLandmark)
        {
            SplitRockPrefabsByTier();
            GameObject[] pool = cliffPrefabsCache.Length > 0 ? cliffPrefabsCache : groupPrefabsCache;
            if (pool.Length == 0) pool = singlePrefabsCache;
            GameObject landmarkPrefab = pool[rng.Next(pool.Length)];
            return InstantiateRock(landmarkPrefab, "Rock_Landmark", 2.4f, 4.2f, rng);
        }
        GameObject prefab = PickRockPrefab(rng, ruggedness);
        return InstantiateRock(prefab, "Rock", 0.6f, 1.8f, rng);
    }

    // talus field처럼 큰 바위 주변에 흩어지는 작은 자갈
    static GameObject CreateGravelPlaceholder(System.Random rng)
    {
        SplitRockPrefabsByTier();
        GameObject prefab = singlePrefabsCache[rng.Next(singlePrefabsCache.Length)];
        return InstantiateRock(prefab, "Gravel", 0.12f, 0.4f, rng);
    }

    // 냉수성 산호: 가지가 중심에서 방사형으로 뻗는 형태 (광합성 없이 사니 조류 색소 대신 골격 색 그대로)
    static GameObject CreateCoralPlaceholder(System.Random rng)
    {
        GameObject root = new GameObject("Coral");
        int branchCount = rng.Next(4, 8);
        Color tint = DeepCoralColor(rng);

        for (int i = 0; i < branchCount; i++)
        {
            GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            branch.name = "CoralBranch";
            branch.transform.SetParent(root.transform, false);

            float length = Lerp01(rng, 0.6f, 1.8f);
            float thickness = Lerp01(rng, 0.08f, 0.22f);
            branch.transform.localRotation = Quaternion.Euler(Lerp01(rng, 10f, 45f), (float)rng.NextDouble() * 360f, 0f);
            branch.transform.localScale = new Vector3(thickness, length * 0.5f, thickness);
            branch.transform.localPosition = branch.transform.up * length * 0.4f;

            var renderer = branch.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = tint;
            renderer.sharedMaterial = mat;
        }

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "Coral";
        info.displayName = "산호";

        return root;
    }

    // 빨강/금빛·베이지/흰색 중 랜덤 (냉수성 산호에서 흔한 골격 색 계열)
    static Color DeepCoralColor(System.Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.35) return Color.HSVToRGB(0.0f, Lerp01(rng, 0.55f, 0.75f), Lerp01(rng, 0.55f, 0.8f));
        if (roll < 0.6) return Color.HSVToRGB(0.12f, Lerp01(rng, 0.35f, 0.55f), Lerp01(rng, 0.7f, 0.9f));
        return Color.HSVToRGB(0.0f, 0.0f, Lerp01(rng, 0.85f, 0.97f));
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // 같은 무더기 안에서 돌끼리 너무 붙지 않도록 최소 간격을 두고 위치를 뽑음
    static Vector2 PickSpacedOffset(System.Random rng, float radius, float minSpacing, List<Vector2> placed)
    {
        Vector2 candidate = Vector2.zero;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            candidate = new Vector2(Lerp01(rng, -radius, radius), Lerp01(rng, -radius, radius));
            bool farEnough = true;
            foreach (var p in placed)
            {
                if (Vector2.Distance(candidate, p) < minSpacing) { farEnough = false; break; }
            }
            if (farEnough) break;
        }
        placed.Add(candidate);
        return candidate;
    }

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
