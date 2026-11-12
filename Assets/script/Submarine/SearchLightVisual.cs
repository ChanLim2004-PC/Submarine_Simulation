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

    // 자식으로 달린 탐조등(Searchlight 프리팹)을 켜고 끈다
    private void OnLightChanged(bool isOn)
    {
        foreach (var light in GetComponentsInChildren<SearchlightController>(true))
            light.SetOn(isOn);
    }
}
