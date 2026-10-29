using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// 수심대 기반 구조물(열수분출공/난파선) 배치: 희소 Poisson Disk Sampling + 수심대별 등장 확률
public class StructureScatterGenerator
{
    const int LayoutSeed = 23;
    const float minRadius = 300f;
    const string DepthSettingsPath = "Assets/Settings/Light/DepthSettings.asset";
    const string ProfilePath = "Assets/Settings/Light/UnderwaterProfile.asset";

    [MenuItem("Tools/해저 구조물 배치 (수심대별)")]
    public static void ScatterStructures()
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/SeabedTerrainData.asset");
        GameObject terrainObj = GameObject.Find("SeabedTerrain");
        if (terrainData == null || terrainObj == null)
        {
            EditorUtility.DisplayDialog("구조물 배치 실패", "먼저 해저 지형을 생성해주세요.", "확인");
            return;
        }

        DepthSettings depthSettings = AssetDatabase.LoadAssetAtPath<DepthSettings>(DepthSettingsPath);
        UnderwaterProfile profile = AssetDatabase.LoadAssetAtPath<UnderwaterProfile>(ProfilePath);
        if (depthSettings == null || profile == null)
        {
            EditorUtility.DisplayDialog("구조물 배치 실패", "DepthSettings/UnderwaterProfile 에셋을 찾을 수 없습니다.", "확인");
            return;
        }

        GameObject oldContainer = GameObject.Find("SeabedStructures");
        if (oldContainer != null)
            Object.DestroyImmediate(oldContainer);
        GameObject container = new GameObject("SeabedStructures");

        float width = terrainData.size.x;
        float length = terrainData.size.z;
        Vector3 terrainOrigin = terrainObj.transform.position;

        List<Vector2> points = PoissonDiskSample(width, length, minRadius, LayoutSeed);
        System.Random rng = new System.Random(LayoutSeed + 1);
        int placed = 0;

        foreach (Vector2 p in points)
        {
            float u = p.x / width;
            float v = p.y / length;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            float worldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(u, v);
            float displayDepth = depthSettings.ToDisplayDepth(worldY);
            DepthZone zone = displayDepth < profile.photicBottom ? DepthZone.Photic
                            : displayDepth < profile.twilightBottom ? DepthZone.Twilight
                            : DepthZone.Midnight;

            double spawnChance = zone == DepthZone.Photic ? 0.05 : zone == DepthZone.Twilight ? 0.35 : 0.6;
            if (rng.NextDouble() > spawnChance) continue;

            bool isVent = zone == DepthZone.Midnight ? rng.NextDouble() < 0.7 : rng.NextDouble() < 0.3;

            Vector3 worldPos = new Vector3(terrainOrigin.x + p.x, worldY, terrainOrigin.z + p.y);
            GameObject structure = isVent ? CreateVentPlaceholder(rng) : CreateWreckPlaceholder(rng);
            structure.transform.SetParent(container.transform, true);
            structure.transform.position = worldPos;
            structure.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            placed++;
        }

        Debug.Log($"[구조물 배치] {placed}개 배치 완료.");
        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // 보유 중인 무료 바위팩의 Cliff 프리팹을 세로로 늘려서 굴뚝으로 사용 + 검은 연기 파티클 + 글로우
    static GameObject CreateVentPlaceholder(System.Random rng)
    {
        GameObject root = new GameObject("HydrothermalVent");
        float h = Lerp01(rng, 3f, 6f);

        GameObject cliffPrefab = FindCliffPrefab();
        if (cliffPrefab != null)
        {
            GameObject chimney = Object.Instantiate(cliffPrefab);
            chimney.name = "VentChimney";
            chimney.transform.SetParent(root.transform, false);
            chimney.transform.localScale = new Vector3(Lerp01(rng, 0.8f, 1.3f), h, Lerp01(rng, 0.8f, 1.3f));
            chimney.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            Material mat = PropScatterGenerator.RockMaterialFor(PropScatterGenerator.RockTextureSetFor(cliffPrefab.name));
            foreach (var renderer in chimney.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = mat;
        }

        GameObject smokeObj = new GameObject("VentSmoke");
        smokeObj.transform.SetParent(root.transform, false);
        smokeObj.transform.localPosition = new Vector3(0f, h * 1.8f, 0f);
        var ps = smokeObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = 8f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.5f;

        GameObject glow = new GameObject("VentGlow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, h * 1.2f, 0f);
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.4f, 0.2f);
        light.intensity = 3f;
        light.range = 30f;

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "Vent";
        info.displayName = "열수분출공";
        return root;
    }

    static GameObject[] cliffPrefabCache;

    static GameObject FindCliffPrefab()
    {
        if (cliffPrefabCache == null)
        {
            var all = PropScatterGenerator.LoadRockPrefabs();
            var cliffs = new List<GameObject>();
            foreach (var p in all) if (p.name.Contains("Cliff")) cliffs.Add(p);
            cliffPrefabCache = cliffs.Count > 0 ? cliffs.ToArray() : all;
        }
        return cliffPrefabCache.Length > 0 ? cliffPrefabCache[0] : null;
    }

    const string MaritimePrefabFolder = "Assets/Maritime_Heritage/Prefabs";
    static GameObject shipBodyPrefab, sailPrefab, anchorPrefab;
    static GameObject[] cargoPrefabCache;

    static void LoadMaritimePrefabs()
    {
        if (cargoPrefabCache != null) return;
        shipBodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MaritimePrefabFolder}/SM_Mado_ship_body.prefab");
        sailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MaritimePrefabFolder}/SM_Mado_Sail.prefab");
        anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MaritimePrefabFolder}/SM_Mado_Anchor.prefab");

        string[] cargoNames = {
            "SM_Mado_PotteryA", "SM_Mado_PotteryB", "SM_Mado_PotteryC", "SM_Mado_PotteryD", "SM_Mado_PotteryE",
            "SM_Mado_Jal", "SM_Mado_Bowlbundle", "SM_Mado_Burner",
            "SM_Mado_BacketA", "SM_Mado_BacketB", "SM_Mado_BacketC",
            "SM_Mado_Ricebag", "SM_Mado_Ricebags", "SM_Mado_Stones"
        };
        var list = new List<GameObject>();
        foreach (var n in cargoNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MaritimePrefabFolder}/{n}.prefab");
            if (prefab != null) list.Add(prefab);
        }
        cargoPrefabCache = list.ToArray();
    }

    // 마도선(태안 마도 해역 고려시대 난파선) 에셋으로 3가지 유형을 랜덤 구성: 온전한 선체 / 파손된 선체+화물 유출 / 선체 없이 화물만
    static GameObject CreateWreckPlaceholder(System.Random rng)
    {
        LoadMaritimePrefabs();
        GameObject root = new GameObject("Wreckage");

        double variant = rng.NextDouble();
        if (variant < 0.15)
        {
            SpawnHull(root, rng, includeSail: true, includeAnchor: true, tiltMax: 15f, sinkDepth: 0.3f);
            SpawnCargoScatter(root, rng, rng.Next(1, 3), 6f);
        }
        else if (variant < 0.70)
        {
            SpawnHull(root, rng, includeSail: false, includeAnchor: rng.NextDouble() < 0.4, tiltMax: 40f, sinkDepth: 1.2f);
            SpawnCargoScatter(root, rng, rng.Next(3, 9), 10f);
        }
        else
        {
            SpawnCargoScatter(root, rng, rng.Next(5, 13), 9f);
        }

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "Wreck";
        info.displayName = "난파선 잔해";
        return root;
    }

    static void SpawnHull(GameObject root, System.Random rng, bool includeSail, bool includeAnchor, float tiltMax, float sinkDepth)
    {
        if (shipBodyPrefab == null) return;

        GameObject hull = Object.Instantiate(shipBodyPrefab);
        hull.name = "WreckHull";
        hull.transform.SetParent(root.transform, false);
        hull.transform.localScale = Vector3.one * Lerp01(rng, 0.85f, 1.15f);
        hull.transform.localPosition = new Vector3(0f, -sinkDepth, 0f);
        hull.transform.localRotation = Quaternion.Euler(Lerp01(rng, -tiltMax, tiltMax), (float)rng.NextDouble() * 360f, Lerp01(rng, -tiltMax * 0.5f, tiltMax * 0.5f));

        if (includeSail && sailPrefab != null)
        {
            GameObject sail = Object.Instantiate(sailPrefab);
            sail.name = "WreckSail";
            sail.transform.SetParent(hull.transform, false);
        }
        if (includeAnchor && anchorPrefab != null)
        {
            GameObject anchor = Object.Instantiate(anchorPrefab);
            anchor.name = "WreckAnchor";
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(Lerp01(rng, -5f, 5f), 0f, Lerp01(rng, -5f, 5f));
            anchor.transform.localRotation = Quaternion.Euler(Lerp01(rng, 0f, 90f), (float)rng.NextDouble() * 360f, 0f);
        }
    }

    static void SpawnCargoScatter(GameObject root, System.Random rng, int count, float radius)
    {
        if (cargoPrefabCache.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = cargoPrefabCache[rng.Next(cargoPrefabCache.Length)];
            GameObject cargo = Object.Instantiate(prefab);
            cargo.name = "WreckCargo";
            cargo.transform.SetParent(root.transform, false);
            cargo.transform.localPosition = new Vector3(Lerp01(rng, -radius, radius), 0f, Lerp01(rng, -radius, radius));
            cargo.transform.localRotation = Quaternion.Euler(Lerp01(rng, -10f, 10f), (float)rng.NextDouble() * 360f, Lerp01(rng, -10f, 10f));
            cargo.transform.localScale = Vector3.one * Lerp01(rng, 0.9f, 1.3f);
        }
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static List<Vector2> PoissonDiskSample(float width, float height, float radius, int seed)
    {
        var rng = new System.Random(seed);
        float cellSize = radius / Mathf.Sqrt(2f);
        int gridW = Mathf.CeilToInt(width / cellSize);
        int gridH = Mathf.CeilToInt(height / cellSize);
        int[,] grid = new int[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                grid[x, y] = -1;

        List<Vector2> points = new List<Vector2>();
        List<int> active = new List<int>();

        Vector2 first = new Vector2((float)rng.NextDouble() * width, (float)rng.NextDouble() * height);
        AddPoint(first, points, active, grid, cellSize, gridW, gridH);

        const int k = 20;
        while (active.Count > 0)
        {
            int listIdx = rng.Next(active.Count);
            int idx = active[listIdx];
            Vector2 center = points[idx];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = radius * (1f + (float)rng.NextDouble());
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                if (candidate.x < 0f || candidate.x >= width || candidate.y < 0f || candidate.y >= height)
                    continue;

                if (IsFarEnough(candidate, radius, points, grid, cellSize, gridW, gridH))
                {
                    AddPoint(candidate, points, active, grid, cellSize, gridW, gridH);
                    found = true;
                }
            }

            if (!found)
                active.RemoveAt(listIdx);
        }

        return points;
    }

    static void AddPoint(Vector2 p, List<Vector2> points, List<int> active, int[,] grid, float cellSize, int gridW, int gridH)
    {
        points.Add(p);
        active.Add(points.Count - 1);
        int gx = Mathf.Clamp((int)(p.x / cellSize), 0, gridW - 1);
        int gy = Mathf.Clamp((int)(p.y / cellSize), 0, gridH - 1);
        grid[gx, gy] = points.Count - 1;
    }

    static bool IsFarEnough(Vector2 candidate, float radius, List<Vector2> points, int[,] grid, float cellSize, int gridW, int gridH)
    {
        int gx = Mathf.Clamp((int)(candidate.x / cellSize), 0, gridW - 1);
        int gy = Mathf.Clamp((int)(candidate.y / cellSize), 0, gridH - 1);
        int searchRadius = 2;
        for (int x = gx - searchRadius; x <= gx + searchRadius; x++)
        {
            if (x < 0 || x >= gridW) continue;
            for (int y = gy - searchRadius; y <= gy + searchRadius; y++)
            {
                if (y < 0 || y >= gridH) continue;
                int pi = grid[x, y];
                if (pi < 0) continue;
                if (Vector2.Distance(candidate, points[pi]) < radius)
                    return false;
            }
        }
        return true;
    }
}
