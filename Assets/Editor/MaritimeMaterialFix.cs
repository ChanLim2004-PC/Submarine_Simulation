using UnityEngine;
using UnityEditor;

// Maritime_Heritage 에셋이 Built-in Standard 셰이더라 URP에서 핑크로 보이는 문제 해결
// 기존 머티리얼의 _MainTex/_BumpMap을 그대로 읽어서 URP/Lit로 교체 (SpecGlossMap 등은 단순화를 위해 생략)
public class MaritimeMaterialFix
{
    const string MaterialsRoot = "Assets/Maritime_Heritage/Materials";

    [MenuItem("Tools/난파선 머티리얼 URP 변환")]
    public static void ConvertMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot });
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        int converted = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            Texture baseMap = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;

            mat.shader = urpLit;
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[난파선 머티리얼] {converted}개 URP/Lit로 변환 완료.");
    }
}
