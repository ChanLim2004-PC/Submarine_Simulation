using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 물속에서 본 수면을 UnderwaterSystem 프리팹에 넣는다 (씬은 안 건드림). 메뉴: Light > 9
public static class WaterSurfaceBuilder
{
    const string ShaderName = "Light/WaterSurfaceUnderside";
    const string MaterialPath = "Assets/Materials/Light/WaterSurface.mat";
    const string MeshPath = "Assets/Materials/Light/WaterSurfaceGrid.asset";
    const string OldMeshPath = "Assets/Materials/Light/WaterSurfaceMesh.asset";
    const string ChildName = "WaterSurface";

    // 카메라를 따라다니는 격자. 셰이더가 간격 단위로 옮기므로 재질에도 넘긴다
    const int Resolution = 256;          // 256 × 256 = 65,536 (16비트 인덱스 한도)
    const float GridSize = 300f;         // m. 그 밖은 어차피 물색에 묻힌다
    const float CellSize = GridSize / (Resolution - 1);

    [MenuItem("Light/9. 수면 (물속에서 본 물결)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("수면", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        var shader = Shader.Find(ShaderName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnderwaterSystemBuilder.PrefabPath);
        if (shader == null || prefab == null)
        {
            EditorUtility.DisplayDialog("수면", "수면 셰이더 또는 UnderwaterSystem 프리팹을 찾을 수 없습니다. Console의 셰이더 에러를 확인해 주세요.", "확인");
            return;
        }

        var mesh = GetOrCreateGrid();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = shader;
        mat.SetFloat("_GridCell", CellSize);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var t = root.transform.Find(ChildName);
            var go = t != null ? t.gameObject : new GameObject(ChildName);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // 수면이 바닥에 그림자를 드리우지 않게
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 예전 사각형 메시는 더 이상 안 쓴다
        if (AssetDatabase.LoadAssetAtPath<Mesh>(OldMeshPath) != null)
            AssetDatabase.DeleteAsset(OldMeshPath);

        Debug.Log($"[수면] 격자 {Resolution}×{Resolution} ({GridSize}m), UnderwaterSystem 프리팹의 {ChildName} 갱신");
    }

    // 평평한 격자. 높이·위치는 셰이더가 정하므로 바운드를 넉넉히
    static Mesh GetOrCreateGrid()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh != null) return mesh;

        int n = Resolution;
        float h = GridSize * 0.5f;
        var verts = new Vector3[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
                verts[z * n + x] = new Vector3(-h + x * CellSize, 0f, -h + z * CellSize);

        var tris = new int[(n - 1) * (n - 1) * 6];
        int ti = 0;
        for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                int i0 = z * n + x, i1 = i0 + n, i2 = i1 + 1, i3 = i0 + 1;
                // 위에서 보면 시계 방향 = 윗면이 앞면
                tris[ti++] = i0; tris[ti++] = i1; tris[ti++] = i2;
                tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i3;
            }

        mesh = new Mesh { name = "WaterSurfaceGrid" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }
}
