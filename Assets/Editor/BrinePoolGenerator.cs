using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// 브라인풀: 고농도 염수가 고인 웅덩이 - 평평하고 깊은 저지대를 찾아 어둡고 매끈한 수면 + 흰 광물 테두리로 표현
public class BrinePoolGenerator
{
    const int LayoutSeed = 23;
    const float MinSpacing = 150f;

    [MenuItem("Tools/해저 브라인풀 배치")]
    public static void ScatterBrinePools()
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/SeabedTerrainData.asset");
        GameObject terrainObj = GameObject.Find("SeabedTerrain");
        if (terrainData == null || terrainObj == null)
        {
            EditorUtility.DisplayDialog("브라인풀 배치 실패", "먼저 Tools > 해저 지형 생성 (Block-out) 으로 지형을 만들어주세요.", "확인");
            return;
        }

        GameObject oldContainer = GameObject.Find("SeabedBrinePools");
        if (oldContainer != null) Object.DestroyImmediate(oldContainer);
        GameObject container = new GameObject("SeabedBrinePools");

        System.Random rng = new System.Random(LayoutSeed);
        float width = terrainData.size.x;
        float length = terrainData.size.z;
        Vector3 terrainOrigin = terrainObj.transform.position;

        List<Vector2> spots = FindFlatLowSpots(terrainData, width, length);
        Shuffle(spots, rng);

        int count = Mathf.Min(rng.Next(2, 4), spots.Count); // 2~3개
        int placed = 0;
        var placedSpots = new List<Vector2>(); // spots를 순회 중에 직접 수정하면 InvalidOperationException이 나서 별도 리스트로 분리
        foreach (Vector2 p in spots)
        {
            if (placed >= count) break;
            bool farEnough = true;
            for (int i = 0; i < placedSpots.Count; i++)
            {
                if (Vector2.Distance(p, placedSpots[i]) < MinSpacing) { farEnough = false; break; }
            }
            if (!farEnough) continue;

            float u = p.x / width;
            float v = p.y / length;
            float worldY = terrainOrigin.y + terrainData.GetInterpolatedHeight(u, v);
            Vector3 worldPos = new Vector3(terrainOrigin.x + p.x, worldY, terrainOrigin.z + p.y);

            GameObject pool = CreateBrinePool(rng);
            pool.transform.SetParent(container.transform, true);
            pool.transform.position = worldPos;
            placedSpots.Add(p);
            placed++;
        }

        Debug.Log($"[브라인풀] {placed}개 배치 완료.");
        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // 경사도와 높이가 모두 낮은(평평하고 깊은) 지점만 후보로 모음
    static List<Vector2> FindFlatLowSpots(TerrainData terrainData, float width, float length)
    {
        var candidates = new List<Vector2>();
        const int gridSteps = 40;
        for (int gx = 2; gx < gridSteps - 2; gx++)
        {
            for (int gy = 2; gy < gridSteps - 2; gy++)
            {
                float u = (float)gx / gridSteps;
                float v = (float)gy / gridSteps;
                float steepness01 = terrainData.GetSteepness(u, v) / 90f;
                float height01 = terrainData.GetInterpolatedHeight(u, v) / terrainData.size.y;
                if (steepness01 < 0.05f && height01 < 0.3f)
                    candidates.Add(new Vector2(u * width, v * length));
            }
        }
        return candidates;
    }

    static void Shuffle(List<Vector2> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static GameObject CreateBrinePool(System.Random rng)
    {
        GameObject root = new GameObject("BrinePool");
        float radius = Lerp01(rng, 6f, 12f);

        // 수면: Quad를 눕혀서 어둡고 매끈한(반사감 있는) 염수 표현. 기존 코드와 동일하게
        // 투명 셰이더 대신 불투명 + 높은 Smoothness만으로 처리해서 셰이더 리스크를 피함
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "BrineSurface";
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.transform.SetParent(root.transform, false);
        surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        surface.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        surface.transform.localPosition = new Vector3(0f, 0.05f, 0f); // 바닥과 겹쳐 지지직거리지 않도록 살짝 띄움

        var surfaceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        surfaceMat.SetColor("_BaseColor", new Color(0.03f, 0.05f, 0.04f));
        surfaceMat.SetFloat("_Smoothness", 0.95f);
        surfaceMat.SetFloat("_Metallic", 0.05f);
        surface.GetComponent<MeshRenderer>().sharedMaterial = surfaceMat;

        // 가장자리: 염분 결정이 쌓인 흰 테두리를 기존 바위팩 바위를 흰 톤으로 둘러서 표현
        int rimCount = rng.Next(10, 16);
        var rockPrefabs = PropScatterGenerator.LoadRockPrefabs();
        for (int i = 0; i < rimCount; i++)
        {
            GameObject prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
            GameObject rim = Object.Instantiate(prefab);
            rim.name = "BrineRimRock";
            rim.transform.SetParent(root.transform, false);

            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = radius * Lerp01(rng, 0.92f, 1.15f);
            rim.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            rim.transform.localScale = Vector3.one * Lerp01(rng, 0.3f, 0.7f);
            rim.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            var rimMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D bc = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/StylizedRocksPackFREE/Textures/Rocks/T_RockPack_{PropScatterGenerator.RockTextureSetFor(prefab.name)}_BC.tga");
            if (bc != null) rimMat.SetTexture("_BaseMap", bc);
            rimMat.SetColor("_BaseColor", new Color(0.85f, 0.83f, 0.78f));
            foreach (var renderer in rim.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = rimMat;
        }

        // 기포: 가끔 올라오는 작은 입자
        GameObject bubbles = new GameObject("BrineBubbles");
        bubbles.transform.SetParent(root.transform, false);
        bubbles.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        var ps = bubbles.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(0.7f, 0.8f, 0.75f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.maxParticles = 20;
        var emission = ps.emission;
        emission.rateOverTime = 1.5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.8f;

        var info = root.AddComponent<OceanPropInfo>();
        info.category = "BrinePool";
        info.displayName = "브라인풀";

        return root;
    }

    static float Lerp01(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
