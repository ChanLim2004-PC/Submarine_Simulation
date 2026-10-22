using UnityEditor;     // 에디터 전용 기능 (인스펙터 꾸미기)
using UnityEngine;     // 기본 Unity 타입

// DepthZoneSystem 인스펙터 아래쪽에 현재 수심·수심대·적용 중인 값을 실시간으로 보여준다 (읽기 전용)
[CustomEditor(typeof(DepthZoneSystem))]
public class DepthZoneSystemEditor : Editor
{
    public override bool RequiresConstantRepaint() => true;   // 잠수정을 움직이는 동안 계속 갱신

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();                                // 기본 항목 (설정, 대상, 태양)

        var sys = (DepthZoneSystem)target;                     // 이 인스펙터가 보여주는 컴포넌트
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("현재 상태 (자동 계산, 읽기 전용)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))              // 회색으로 표시 (수정 불가)
        {
            EditorGUILayout.FloatField("표시 수심 (m)", sys.CurrentDepth);
            EditorGUILayout.TextField("수심대", ZoneLabel(sys.CurrentZone));
            EditorGUILayout.ColorField("물색", sys.CurrentWaterColor);
            EditorGUILayout.Vector3Field("감쇠 σ (R,G,B)", sys.CurrentSigma);
            EditorGUILayout.FloatField("주변광 세기", sys.CurrentAmbient);
            EditorGUILayout.FloatField("태양 세기", sys.CurrentSunIntensity);
            EditorGUILayout.FloatField("입자 수", sys.CurrentParticleDensity);
        }
    }

    // 수심대 이름을 한국어로
    static string ZoneLabel(DepthZone zone)
    {
        switch (zone)
        {
            case DepthZone.Photic: return "유광층 (0~200m)";
            case DepthZone.Twilight: return "약광층 (200~1,000m)";
            default: return "무광층 (1,000m~)";
        }
    }
}
