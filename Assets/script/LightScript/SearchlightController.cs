using System.Collections.Generic;
using UnityEngine;

// 잠수정 탐조등. Searchlight 프리팹 루트에 붙어 있고, 잠수정 앞쪽에 자식으로 달아서 쓴다
// 조작 파트는 키 입력에서 Toggle()/SetOn()을 부르고, 배터리 계산 때 PowerDraw를 읽으면 된다
[ExecuteAlways]
public class SearchlightController : MonoBehaviour
{
    static readonly List<SearchlightController> active = new List<SearchlightController>();

    // 씬에 켜져 있는 탐조등 목록 (LightingQuery가 조명 판정에 사용)
    public static IReadOnlyList<SearchlightController> Active => active;

    [SerializeField] bool isOn = true;

    [Tooltip("켜져 있을 때 초당 배터리 소모량 (단위는 배터리 쪽에서 정함)")]
    [SerializeField] float powerWhenOn = 0.5f;

    [Tooltip("비워 두면 자식의 Light를 전부 사용")]
    public Light[] Lights;

    public bool IsOn => isOn;
    public float PowerDraw => isOn ? powerWhenOn : 0f;

    void Awake()
    {
        CollectLights();
        ApplyState();
    }

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    void OnValidate()
    {
        // 인스펙터 체크박스로 바로 켜고 끌 수 있게
        CollectLights();
        ApplyState();
    }

    public void SetOn(bool on)
    {
        isOn = on;
        ApplyState();
    }

    public void Toggle() => SetOn(!isOn);

    void CollectLights()
    {
        if (Lights == null || Lights.Length == 0)
            Lights = GetComponentsInChildren<Light>(true);
    }

    void ApplyState()
    {
        if (Lights == null) return;
        foreach (var l in Lights)
            if (l != null) l.enabled = isOn;
    }
}
