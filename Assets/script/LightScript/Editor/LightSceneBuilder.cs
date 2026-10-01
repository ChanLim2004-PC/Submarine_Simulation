using System.IO;                                   // 파일 존재 여부 확인
using UnityEditor;                                 // 에디터 전용 기능 (메뉴, 에셋 저장)
using UnityEditor.SceneManagement;                 // 씬 생성·저장
using UnityEngine;                                 // 기본 Unity 타입
using UnityEngine.Rendering.Universal;             // URP 카메라 설정 (Post Processing)

// 빛·시야 파트 테스트 씬(LightScene)을 자동으로 만들어 주는 에디터 도구
// 사용법: Unity 상단 메뉴 Light > 1. 테스트 씬 만들기 (LightScene)
public static class LightSceneBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";          // 만들 씬 경로
    const string MaterialFolder = "Assets/Materials/Light/Test";        // 테스트용 머티리얼 폴더

    static readonly float[] Distances = { 2f, 5f, 10f, 20f, 40f };      // 카메라~큐브 묶음 거리 (m)
    static readonly float[] Bearings = { -30f, -15f, 0f, 15f, 30f };     // 각 묶음의 좌우 방향 (도), 서로 가리지 않게 부채꼴로 배치

    [MenuItem("Light/1. 테스트 씬 만들기 (LightScene)")]
    public static void Build()
    {
        // Play 모드 중에는 씬을 만들 수 없으므로 안내 후 중단
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("LightScene", "Play 모드를 끈 뒤 다시 실행해 주세요.\n(화면 위 가운데 ▶ 버튼을 한 번 더 누르면 꺼집니다)", "확인");
            return;
        }

        // 이미 씬이 있으면 덮어쓸지 확인
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("LightScene", "LightScene이 이미 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
            return;

        // 지금 열린 씬에 저장 안 한 변경이 있으면 저장할지 물어봄 (취소하면 중단)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // URP Lit 셰이더 찾기 (없으면 URP 설정 문제이므로 중단)
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { Debug.LogError("[LightScene] URP Lit 셰이더를 찾을 수 없습니다."); return; }

        EnsureFolder(MaterialFolder);                                    // 머티리얼 폴더가 없으면 만들기

        // 테스트용 머티리얼 (모두 불투명 URP Lit → 깊이를 기록하므로 L1 감쇠가 걸림)
        Material white = GetOrCreateMaterial(lit, "Test_White", new Color(1f, 1f, 1f));
        Material red   = GetOrCreateMaterial(lit, "Test_Red",   new Color(1f, 0f, 0f));
        Material green = GetOrCreateMaterial(lit, "Test_Green", new Color(0f, 1f, 0f));
        Material blue  = GetOrCreateMaterial(lit, "Test_Blue",  new Color(0f, 0f, 1f));
        Material floorMat = GetOrCreateMaterial(lit, "Test_Floor", new Color(0.55f, 0.52f, 0.45f)); // 모래색 바닥
        Material subMat   = GetOrCreateMaterial(lit, "Test_Submarine", new Color(0.85f, 0.75f, 0.2f)); // 노란 잠수정
        AssetDatabase.SaveAssets();                                      // 머티리얼을 디스크에 저장

        // 빈 씬 새로 만들기 (카메라·조명 없이 시작)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1) 태양 (Directional Light) — 나중에 L3가 수심에 따라 세기를 줄임 ──
        var sunGO = new GameObject("Sun");                               // 오브젝트 이름
        var sun = sunGO.AddComponent<Light>();                           // 조명 컴포넌트 추가
        sun.type = LightType.Directional;                                // 평행광 (햇빛)
        sun.intensity = 1f;                                              // 기본 세기
        sun.shadows = LightShadows.Soft;                                 // 부드러운 그림자
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);      // 위에서 비스듬히 비춤
        RenderSettings.sun = sun;                                        // 씬의 "태양"으로 등록

        // ── 2) 바닥 평면 ──
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);     // 기본 Plane = 10m x 10m
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -22f, 20f);           // 가장 큰 큐브 묶음보다 아래
        floor.transform.localScale = new Vector3(40f, 1f, 40f);          // 400m x 400m로 확대 (끝이 안 보이게)
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;        // 모래색 적용

        // ── 3) 잠수정 대용 — 빈 부모(이동·수심 추적 대상) + 몸체 큐브 + 카메라 ──
        var sub = new GameObject("Submarine_Proxy");                     // L3가 이 오브젝트의 y로 수심을 계산
        sub.transform.position = new Vector3(0f, -10f, 0f);              // 수면(y=0) 아래 10m

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);       // 눈에 보이는 몸체
        body.name = "Body";
        body.transform.SetParent(sub.transform, false);                  // 잠수정의 자식으로
        body.transform.localScale = new Vector3(1.2f, 0.8f, 2.5f);       // 납작하고 긴 상자 모양
        body.GetComponent<Renderer>().sharedMaterial = subMat;           // 노란색 적용

        // ── 4) 카메라 — 잠수정 뒤쪽 위 (3인칭 대용). 잠수정을 움직이면 같이 따라감 ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";                                        // Camera.main으로 찾을 수 있게
        camGO.transform.SetParent(sub.transform, false);                 // 잠수정의 자식으로
        camGO.transform.localPosition = new Vector3(0f, 2f, -6f);        // 뒤로 6m, 위로 2m
        var cam = camGO.AddComponent<Camera>();                          // 카메라 컴포넌트
        cam.fieldOfView = 60f;                                           // 세로 시야각 60도
        cam.nearClipPlane = 0.1f;                                        // 가까운 면 0.1m
        cam.farClipPlane = 500f;                                         // 먼 면 500m
        camGO.AddComponent<AudioListener>();                             // 소리 듣는 위치 (경고 방지용)
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true; // Post Processing 켬 (2단계 카메라 톤에 필요)

        // ── 5) 색 큐브 — 거리별 묶음 5개, 묶음마다 위에서부터 흰·빨·초·파 ──
        var targets = new GameObject("TestTargets").transform;           // 큐브들을 모아 둘 부모
        Vector3 camPos = camGO.transform.position;                       // 카메라 월드 위치 (거리 기준점)
        Material[] mats = { white, red, green, blue };                   // 위→아래 순서
        string[] colorNames = { "White", "Red", "Green", "Blue" };

        for (int i = 0; i < Distances.Length; i++)
        {
            float d = Distances[i];                                      // 이 묶음의 거리
            float size = Mathf.Max(0.25f, d * 0.12f);                    // 큐브 한 변: 거리에 비례 → 화면에서 비슷한 크기로 보임
            Quaternion dir = Quaternion.Euler(0f, Bearings[i], 0f);      // 이 묶음의 좌우 방향

            var group = new GameObject($"D{d:00}m").transform;           // 예: D02m, D40m
            group.SetParent(targets, false);
            group.position = camPos + dir * Vector3.forward * d;         // 카메라에서 d만큼 떨어진 곳
            group.rotation = dir;                                        // 큐브 정면이 카메라를 향하게

            for (int j = 0; j < mats.Length; j++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"{colorNames[j]}_{d:00}m";                  // 예: Red_10m
                cube.transform.SetParent(group, false);
                cube.transform.localPosition = new Vector3(0f, (1.5f - j) * size * 1.25f, 0f); // 4개를 세로로 쌓고 사이를 살짝 띄움
                cube.transform.localScale = Vector3.one * size;          // 크기 적용
                cube.GetComponent<Renderer>().sharedMaterial = mats[j];  // 색 적용
            }
        }

        // 씬 저장 후 알림
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[LightScene] 테스트 씬 생성 완료: {ScenePath}");
    }

    // 머티리얼이 이미 있으면 불러오고, 없으면 새로 만든 뒤 색을 맞춤
    static Material GetOrCreateMaterial(Shader shader, string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";                    // 저장 경로
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);         // 기존 머티리얼 찾기
        if (mat == null)
        {
            mat = new Material(shader);                                  // 새 URP Lit 머티리얼
            AssetDatabase.CreateAsset(mat, path);                        // 에셋 파일로 저장
        }
        mat.SetColor("_BaseColor", color);                               // 기본 색
        mat.SetFloat("_Smoothness", 0.2f);                               // 반들거림을 낮춰 하이라이트가 색 판단을 방해하지 않게
        EditorUtility.SetDirty(mat);                                     // 변경 사항 저장 표시
        return mat;
    }

    // "Assets/A/B/C" 같은 경로의 폴더를 차례로 만듦 (이미 있으면 건너뜀)
    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');                                // ["Assets","Materials","Light","Test"]
        string current = parts[0];                                       // "Assets"부터 시작
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))                      // 폴더가 없으면
                AssetDatabase.CreateFolder(current, parts[i]);           // 새로 만듦
            current = next;
        }
    }
}
