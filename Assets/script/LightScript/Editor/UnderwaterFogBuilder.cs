using UnityEditor;                                 // 에디터 전용 기능 (메뉴, 에셋 저장)
using UnityEditor.SceneManagement;                 // 씬 저장
using UnityEngine;                                 // 기본 Unity 타입
using UnityEngine.Rendering.Universal;             // URP Renderer Data, Full Screen Pass

// 수중 감쇠 — 머티리얼 생성 + PC_Renderer에 Full Screen Pass 추가 + LightScene에 확인용 오브젝트 배치
// 사용법: LightScene을 연 상태에서 메뉴 Light > 3. 수중 감쇠 (UnderwaterFog)
public static class UnderwaterFogBuilder
{
    const string ScenePath = "Assets/Scenes/LightScene.unity";                    // 테스트 씬
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";              // Windows에서 쓰이는 공용 Renderer Data
    const string ShaderName = "Light/UnderwaterFog";                              // 셰이더 이름 (UnderwaterFog.shader 첫 줄)
    const string MaterialFolder = "Assets/Materials/Light";                       // 빛 파트 머티리얼 폴더
    const string MaterialPath = MaterialFolder + "/UnderwaterFog.mat";            // 만들 머티리얼 경로
    public const string FeatureName = "UnderwaterFog";                            // Renderer Feature 이름 (나중에 효과 토글 키가 이 이름으로 찾음)

    [MenuItem("Light/3. 수중 감쇠 (UnderwaterFog)")]
    public static void Build()
    {
        // Play 모드 중에는 에셋·씬을 바꿀 수 없으므로 중단
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("수중 감쇠", "Play 모드를 끈 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        // LightScene이 열려 있는지 확인
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("수중 감쇠", "LightScene을 먼저 열어 주세요.", "확인");
            return;
        }

        // ── 1) 머티리얼 준비 ──
        var shader = Shader.Find(ShaderName);                                     // 셰이더 찾기
        if (shader == null) { Debug.LogError($"[수중 감쇠] 셰이더 {ShaderName}를 찾을 수 없습니다. Console의 셰이더 에러를 확인하세요."); return; }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))                         // 폴더 없으면 생성
            AssetDatabase.CreateFolder("Assets/Materials", "Light");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);          // 기존 머티리얼 찾기
        if (mat == null)
        {
            mat = new Material(shader);                                           // 새 머티리얼
            AssetDatabase.CreateAsset(mat, MaterialPath);                         // 에셋으로 저장
        }
        mat.shader = shader;                                                      // 셰이더 연결 보장

        // ── 2) 공용 Renderer Data에 Full Screen Pass 추가 ──
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null) { Debug.LogError($"[수중 감쇠] {RendererPath}를 찾을 수 없습니다."); return; }

        // 이미 추가돼 있으면 새로 만들지 않고 설정만 갱신
        FullScreenPassRendererFeature feature = null;
        foreach (var f in data.rendererFeatures)
            if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) feature = fs;

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();   // 새 Feature 생성
            feature.name = FeatureName;                                                   // 인스펙터에 보일 이름
            AssetDatabase.AddObjectToAsset(feature, data);                                // Renderer Data 파일 안에 저장
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId); // 파일 내부 ID

            // Unity 인스펙터의 "Add Renderer Feature" 버튼과 같은 방식으로 목록에 등록
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");                    // Feature 목록
            var map = so.FindProperty("m_RendererFeatureMap");                    // Feature ID 목록 (목록과 짝을 이룸)
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();                                         // 변경 반영
        }

        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents; // 불투명 물체 다음, 반투명 물체 전에 적용
        feature.fetchColorBuffer = true;                                          // 원래 화면 색을 _BlitTexture로 받음
        feature.requirements = ScriptableRenderPassInput.Depth;                  // 깊이 텍스처 필요
        feature.passMaterial = mat;                                               // 위 머티리얼 사용
        feature.passIndex = 0;                                                    // 셰이더의 첫 번째 Pass
        feature.SetActive(true);                                                  // 켜기

        EditorUtility.SetDirty(feature);
        data.SetDirty();                                                          // 렌더러가 Feature를 다시 만들도록 알림
        AssetDatabase.SaveAssets();                                               // 디스크에 저장

        // ── 3) 씬에 확인용 오브젝트 배치 (σ·물색을 전역 변수로 넘김) ──
        const string previewName = "UnderwaterFogPreview";
        var go = GameObject.Find(previewName);
        if (go == null) go = new GameObject(previewName);
        if (go.GetComponent<UnderwaterFogPreview>() == null) go.AddComponent<UnderwaterFogPreview>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;                                          // 인스펙터에서 바로 σ·물색 조정 가능
        Debug.Log($"[수중 감쇠] {MaterialPath} 생성, {RendererPath}에 '{FeatureName}' 추가, {previewName} 배치 완료");
    }
}
