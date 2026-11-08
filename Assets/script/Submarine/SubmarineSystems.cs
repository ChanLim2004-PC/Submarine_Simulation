using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SubmarineSystems : MonoBehaviour
{
    [Header("배터리")]
    // 현재 배터리 (%)
    [Range(0f, 100f)]
    public float battery = 100f;

    // 기본 소모량 (%/s)
    public float baseDrainPerSecond = 0.02f;

    // 탐조등 켜져 있을 때 추가 소모량 (%/s)
    public float lightDrainPerSecond = 0.05f;

    [Header("탐조등")]
    // 탐조등 토글 키
    public Key lightToggleKey = Key.L;

    // 탐조등 켜짐 여부 (읽기 전용으로 쓰고, 변경은 SetLight 사용)
    public bool isLightOn = false;

    // 탐조등 상태가 바뀔 때 호출됨 (true = 켜짐). 빛 구현 쪽에서 구독
    public event Action<bool> LightChanged;

    void Start()
    {
        // 시작 상태를 구독자에게 알림
        LightChanged?.Invoke(isLightOn);
    }

    void Update()
    {
        // 탐조등 토글 (배터리가 있을 때만 켤 수 있음)
        if (Keyboard.current != null &&
            Keyboard.current[lightToggleKey].wasPressedThisFrame)
        {
            SetLight(!isLightOn);
        }

        // 배터리 소모
        float drain = baseDrainPerSecond;
        if (isLightOn)
            drain += lightDrainPerSecond;

        battery = Mathf.Max(0f, battery - drain * Time.deltaTime);

        // 배터리가 바닥나면 탐조등 강제 종료
        if (battery <= 0f)
            SetLight(false);
    }

    // 탐조등 켜기/끄기. 상태가 실제로 바뀔 때만 이벤트 발생
    public void SetLight(bool on)
    {
        if (on && battery <= 0f)
            on = false;

        if (isLightOn == on)
            return;

        isLightOn = on;
        LightChanged?.Invoke(isLightOn);
    }
}