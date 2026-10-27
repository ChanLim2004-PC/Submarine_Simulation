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

    static GameObject CreateVentPlaceholder(System.Random rng)
    {
        GameObject root = new GameObject("HydrothermalVent");

        GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cone.name = "VentChimney";
        cone.transform.SetParent(root.transform, false);
        float h = Lerp01(rng, 8f, 20f);
        cone.transform.localScale = new Vector3(Lerp01(rng, 1.5f, 3f), h, Lerp01(rng, 1.5f, 3f));
        cone.transform.localPosition = new Vector3(0f, h, 0f);
        var coneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        coneMat.color = new Color(0.15f, 0.1f, 0.08f);
        cone.GetComponent<MeshRenderer>().sharedMaterial = coneMat;

        GameObject glow = new GameObject("VentGlow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, h * 2f, 0f);
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

    static GameObject CreateWreckPlaceholder(System.Random rng)
    {
        GameObject root = new GameObject("Wreckage");
        int fragments = rng.Next(3, 6);
        for (int i = 0; i < fragments; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.name = "WreckFragment";
            frag.transform.SetParent(root.transform, false);
            frag.transform.localScale = new Vector3(Lerp01(rng, 4f, 12f), Lerp01(rng, 1f, 2.5f), Lerp01(rng, 2f, 5f));
            frag.transform.localPosition = new Vector3(Lerp01(rng, -8f, 8f), Lerp01(rng, 0f, 2f), Lerp01(rng, -8f, 8f));
            frag.transform.localRotation = Quaternion.Euler(Lerp01(rng, -20f, 20f), (float)rng.NextDouble() * 360f, Lerp01(rng, -15f, 15f));
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.35f, 0.22f, 0.15f);
            frag.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "Wreck";
        info.displayName = "난파선 잔해";
        return root;
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
