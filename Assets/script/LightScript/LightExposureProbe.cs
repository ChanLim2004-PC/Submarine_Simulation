using UnityEngine;

// 조명 판정 확인용. 이 오브젝트 위치의 노출값을 Scene 창(구 색 + 숫자)과 Game 창(숫자)에 띄운다
// 빨강 = 어두움, 초록 = 관측 가능한 밝기
public class LightExposureProbe : MonoBehaviour
{
    public bool showInGame = true;

    void OnDrawGizmos()
    {
        float e = LightingQuery.GetLightExposure(transform.position);
        bool lit = e >= LightingQuery.ObservationThreshold;
        Gizmos.color = lit ? Color.green : Color.Lerp(Color.red, Color.yellow, e / LightingQuery.ObservationThreshold);
        Gizmos.DrawSphere(transform.position, 0.3f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"{name}\n노출 {e:0.00} {(lit ? "OK" : "")}");
#endif
    }

    void OnGUI()
    {
        if (!showInGame || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position);
        if (sp.z < 0) return;   // 카메라 뒤
        float e = LightingQuery.GetLightExposure(transform.position);
        GUI.color = e >= LightingQuery.ObservationThreshold ? Color.green : Color.red;
        GUI.Label(new Rect(sp.x - 40, Screen.height - sp.y - 10, 120, 20), $"노출 {e:0.00}");
    }
}
