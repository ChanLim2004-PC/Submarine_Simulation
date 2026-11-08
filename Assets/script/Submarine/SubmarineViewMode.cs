using UnityEngine;
using UnityEngine.InputSystem;

public enum ViewMode
{
    ThirdPerson,   // 이동 모드
    FirstPerson    // 관측 모드
}

public class SubmarineViewMode : MonoBehaviour
{
    // 현재 모드
    public ViewMode mode = ViewMode.ThirdPerson;

    // 모드 전환 키
    public Key toggleKey = Key.V;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            mode = (mode == ViewMode.ThirdPerson)
                ? ViewMode.FirstPerson
                : ViewMode.ThirdPerson;
        }
    }
}