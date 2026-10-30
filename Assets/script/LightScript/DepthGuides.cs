using UnityEngine;

// Scene 창에 수면과 수심대 경계 높이를 표시하는 배치용 안내선 (게임 화면에는 안 보임)
// 높이는 DepthSettings·UnderwaterProfile 값에서 계산하므로 값을 바꾸면 따라 움직인다
public class DepthGuides : MonoBehaviour
{
    public DepthSettings depthSettings;
    public UnderwaterProfile profile;
    [Tooltip("안내선 사각형 한 변 길이 (m)")]
    public float size = 400f;

    void OnDrawGizmos()
    {
        if (depthSettings == null || profile == null) return;
        Vector3 c = transform.position;

        DrawLevel(depthSettings.surfaceY, new Color(0.3f, 0.9f, 1f), "수면 (0m)");
        DrawLevel(WorldY(profile.photicBottom), new Color(0.2f, 0.5f, 1f), $"유광층 → 약광층 ({profile.photicBottom:0}m)");
        DrawLevel(WorldY(profile.twilightBottom), new Color(0.5f, 0.2f, 0.8f), $"약광층 → 무광층 ({profile.twilightBottom:0}m)");

        float WorldY(float displayDepth) => depthSettings.surfaceY - displayDepth / Mathf.Max(0.0001f, depthSettings.depthScale);

        void DrawLevel(float y, Color color, string label)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(new Vector3(c.x, y, c.z), new Vector3(size, 0f, size));
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(new Vector3(c.x - size * 0.5f, y, c.z - size * 0.5f), $"{label}  y={y:0.#}");
#endif
        }
    }
}
