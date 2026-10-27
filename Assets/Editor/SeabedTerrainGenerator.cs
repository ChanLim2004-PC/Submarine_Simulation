using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// 해저 지형 블록아웃 자동 생성 (하나로 연결된 연속 지형: 평원<->해저산<->해구가 같은 ꬠ로 이어짐) + 모래/암석 텍스처 블렌딩
// 실행: Unity 메뉴 Tools -> 해저 지형 생성 (Block-out)
public class SeabedTerrainGenerator
{
    // 지형 전체 레이아웃 시드. 바꾸면 해저산/해구 배치가 통째로 달라짐.
    const int LayoutSeed = 7;

    [MenuItem("Tools/해저 지형 생성 (Block-out)")]
    public static void GenerateSeabedTerrain()
    {
        int resolution = 1025; // Unity heightmap 규칙: 2^n + 1
        float terrainWidth = 2000f;
        float terrainLength = 2000f;
        float terrainHeight = 300f;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = resolution;
        terrainData.alphamapResolution = 512;
        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        System.Random rng = new System.Random(LayoutSeed);

        // 랜드마크급 해저산 (너무 많으면 오히려 산만해지니 소수만, 대신 주변과 자연스럽게 이어지도록 처리)
        int seamountCount = 3;
        Vector4[] seamounts = new Vector4[seamountCount]; // x, z, spread(작을수록 뾰족), height
        for (int i = 0; i < seamountCount; i++)
        {
            float sx = Lerp01(rng, 0.12f, 0.88f);
            float sz = Lerp01(rng, 0.12f, 0.88f);
            float spread = Lerp01(rng, 0.014f, 0.032f);
            float height = Lerp01(rng, 0.30f, 0.46f);
            seamounts[i] = new Vector4(sx, sz, spread, height);
        }

        // 해구/균열
        int trenchCount = 2;
        Vector2[] trenchStart = new Vector2[trenchCount];
        Vector2[] trenchEnd = new Vector2[trenchCount];
        float[] trenchDepth = new float[trenchCount];
        for (int i = 0; i < trenchCount; i++)
        {
            Vector2 a = new Vector2(Lerp01(rng, 0.05f, 0.95f), Lerp01(rng, 0.05f, 0.95f));
            float angle = Lerp01(rng, 0f, 360f) * Mathf.Deg2Rad;
            float length = Lerp01(rng, 0.40f, 0.78f);
            Vector2 b = a + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
            trenchStart[i] = a;
            trenchEnd[i] = b;
            trenchDepth[i] = Lerp01(rng, 0.26f, 0.40f);
        }

        // abyssal hill 능선 방향 (해저 확장 방향에 수직으로 늘어선 줄무늬)
        float ridgeAngle = 35f * Mathf.Deg2Rad;
        float ridgeDirX = Mathf.Cos(ridgeAngle);
        float ridgeDirZ = Mathf.Sin(ridgeAngle);

        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float nx = (float)x / resolution;
                float nz = (float)z / resolution;

                // 전역 도메인 워프: 평원/해저산/해구 전부 이 "같은 휘어짐"을 공유해야
                // 서로 다른 조각을 이어붙인 느낌이 아니라 하나로 연결된 지형처럼 보임
                float wox = Fbm(nx * 1.1f, nz * 1.1f, 4);
                float woz = Fbm(nx * 1.1f + 13.7f, nz * 1.1f + 13.7f, 4);
                float wx = nx + (wox - 0.5f) * 0.30f;
                float wz = nz + (woz - 0.5f) * 0.30f;

                // 거칠기 마스크: 느리게 변하는 기본값 + 해저산에 가까울수록 서서히 거칠어짐.
                // 이 덕분에 "평지 → 거친 비탈 → 봉우리"가 끊기지 않고 이어짐.
                float ruggedMask = Fbm(wx * 0.8f, wz * 0.8f, 4);
                for (int i = 0; i < seamounts.Length; i++)
                {
                    float sx0 = wx - seamounts[i].x;
                    float sz0 = wz - seamounts[i].y;
                    float d02 = sx0 * sx0 + sz0 * sz0;
                    float hotspot = Mathf.Exp(-d02 / (seamounts[i].z * 3.2f));
                    ruggedMask = Mathf.Max(ruggedMask, hotspot);
                }
                ruggedMask = Mathf.SmoothStep(0.15f, 0.9f, ruggedMask);

                // 능선형 다중프랙탈: 평원과 해저산이 같은 노이즈 언어를 공유 -> 질감이 자연스럽게 이어짐
                float ridged = RidgedFbm(wx * 2.3f, wz * 2.3f, 5);
                float h = 0.22f + ridged * ruggedMask * 0.50f;

                // abyssal hill 줄무늬: 평평한 구간일수록 두드러지게 (거친 구간은 ridged가 이미 디테일을 제공)
                float perp = -wx * ridgeDirZ + wz * ridgeDirX;
                float ridgePattern = Mathf.Sin(perp * 70f) * 0.5f + 0.5f;
                h += (ridgePattern - 0.5f) * 0.035f * (1f - ruggedMask);

                // 해저산 정상부 (이미 거칠어진 지역 위에 얹히므로 주변과 자연스럽게 연결됨)
                for (int i = 0; i < seamounts.Length; i++)
                {
                    float sx = wx - seamounts[i].x;
                    float sz = wz - seamounts[i].y;
                    float d2 = sx * sx + sz * sz;
                    float shape = Mathf.Exp(-d2 / seamounts[i].z);
                    h += seamounts[i].w * shape;
                    h += (Fbm(nx * 50f + i, nz * 50f + i, 3) - 0.5f) * 0.03f * shape;
                    h -= 0.04f * Mathf.Exp(-d2 / (seamounts[i].z * 0.06f)); // 정상부 소형 분화구
                }

                // 해구/균열: 같은 워프 좌표계를 쓰기 때문에 지형 전체와 같은 결로 구불거림
                for (int i = 0; i < trenchStart.Length; i++)
                {
                    float meander = (Fbm(nx * 4f + i, nz * 4f + i, 3) - 0.5f) * 0.08f;
                    float t;
                    float dist = DistToSegment(wx + meander, wz + meander,
                        trenchStart[i].x, trenchStart[i].y, trenchEnd[i].x, trenchEnd[i].y, out t);
                    float side = Mathf.Sign((trenchEnd[i].x - trenchStart[i].x) * (wz - trenchStart[i].y)
                        - (trenchEnd[i].y - trenchStart[i].y) * (wx - trenchStart[i].x));
                    float width = side > 0 ? 0.024f : 0.052f; // 비대칭 경사
                    float floorNoise = (Fbm(t * 25f + i, 0.5f, 3) - 0.5f) * 0.03f;
                    float shape = Mathf.Exp(-(dist * dist) / (2f * width * width));
                    h -= (trenchDepth[i] + floorNoise) * shape;
                }

                // 잔잔한 고주파 디테일 (암반 질감)
                h += (Fbm(wx * 60f, wz * 60f, 2) - 0.5f) * 0.012f;

                heights[z, x] = SoftCompress(h);
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

    static float Lerp01(System.Random rng, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
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

    // 능선형(ridged) 다중프랙탈: 평원과 해저산이 "같은 노이즈"에서 나오도록 해서
    // 둘 사이 질감이 자연스럽게 이어지게 만드는 핵심 함수. 값이 클수록 뾰족한 능선.
    static float RidgedFbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.55f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * freq, y * freq);
            n = 1f - Mathf.Abs(n * 2f - 1f); // 0.5 기준으로 접어서 능선처럼 만듦
            n *= n; // 봉우리를 더 날카롭게
            sum += n * amp;
            norm += amp;
            freq *= 2.1f;
            amp *= 0.5f;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    // 점 (px,pz)에서 선분 (ax,az)-(bx,bz)까지의 최단 거리. t는 선분 위 투영 위치(0~1)
    static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz, out float t)
    {
        float abx = bx - ax, abz = bz - az;
        float apx = px - ax, apz = pz - az;
        float ab2 = abx * abx + abz * abz;
        t = ab2 > 0f ? Mathf.Clamp01((apx * abx + apz * abz) / ab2) : 0f;
        float cx = ax + abx * t, cz = az + abz * t;
        float dx = px - cx, dz = pz - cz;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // 0~1 범위를 살짝 벗어나는 값도 평평한 단면 없이 완만하게 눌러주는 압축 함수.
    // 그냥 Clamp01을 쓰면 해구 바닥/해저산 정상부가 평평하게 잘려서 경계가 부자연스러워짐.
    static float SoftCompress(float h)
    {
        float lo = -0.35f, hi = 1.35f;
        float t = Mathf.InverseLerp(lo, hi, h);
        return t * t * t * (t * (t * 6f - 15f) + 10f); // smootherstep
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
        layer.tileSize = new Vector2(60f, 60f);
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();

        return layer;
    }
}
