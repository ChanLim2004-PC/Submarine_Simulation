using UnityEngine;   // 기본 Unity 타입

// 수심 좌표 공용 설정
// 모든 파트(빛·계기판·생물 배치 등)가 같은 수심 값을 쓰도록 이 파일 하나에서만 정의한다
//   수면 = 월드 y (surfaceY), 아래로 갈수록 음수
//   표시 수심(m) = (surfaceY − y) × depthScale  → 이동 거리를 압축해 깊은 바다까지 빨리 내려가게 함
[CreateAssetMenu(fileName = "DepthSettings", menuName = "Light/Depth Settings")]
public class DepthSettings : ScriptableObject
{
    [Tooltip("수면의 월드 y 좌표")]
    public float surfaceY = 0f;                                                      // 수면 높이

    [Tooltip("월드 1m 내려갈 때 표시 수심이 몇 m 늘어나는지 (수심대 판정·계기판 표시에만 사용)")]
    public float depthScale = 5f;                                                    // 수심 압축 배율

    // 월드 y 좌표 → 표시 수심(m). 수면 위는 0
    public float ToDisplayDepth(float worldY)
    {
        return Mathf.Max(0f, (surfaceY - worldY) * depthScale);
    }
}
