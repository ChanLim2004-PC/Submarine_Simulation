using UnityEngine;
using UnityEngine.InputSystem;

// LightScene 테스트용 키 입력. 실제 조작은 조작·UI 파트에서 하므로 다른 씬에는 넣지 않는다
public class SearchlightTestInput : MonoBehaviour
{
    public SearchlightController searchlight;
    public Key toggleKey = Key.L;

    void Update()
    {
        if (searchlight == null || Keyboard.current == null) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            searchlight.Toggle();
    }
}
