using UnityEngine;

// 탐조등의 시각 효과를 담당하는 컴포넌트.
// SubmarineSystems의 켜짐/꺼짐 신호만 받아 처리하므로
// 배터리, 입력, HUD 코드는 건드릴 필요가 없음.
public class SearchLightVisual : MonoBehaviour
{
    // 잠수함의 SubmarineSystems (비워두면 부모에서 자동 탐색)
    public SubmarineSystems systems;

    void Awake()
    {
        if (systems == null)
            systems = GetComponentInParent<SubmarineSystems>();
    }

    void OnEnable()
    {
        if (systems != null)
            systems.LightChanged += OnLightChanged;
    }

    void OnDisable()
    {
        if (systems != null)
            systems.LightChanged -= OnLightChanged;
    }

    // TODO(빛 담당): 여기에 실제 빛 구현을 넣을 것
    // isOn = true면 켜기, false면 끄기
    private void OnLightChanged(bool isOn)
    {
        Debug.Log($"[SearchLight] {(isOn ? "ON" : "OFF")} (빛 미구현)");
    }
}
