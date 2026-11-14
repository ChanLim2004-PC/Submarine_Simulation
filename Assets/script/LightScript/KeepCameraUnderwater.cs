using UnityEngine;

// 카메라가 파도 골 위로 나가 물 밖(물색으로 덮인 하늘)이 보이지 않도록 수면 아래로 붙잡아 둔다
// 메인 카메라에 붙인다. 카메라를 움직이는 스크립트(OrbitCamera)가 위치를 정한 뒤에 실행되도록 순서를 늦춤
[DefaultExecutionOrder(1000)]
public class KeepCameraUnderwater : MonoBehaviour
{
    [Tooltip("가장 깊은 파도 골보다 이만큼 더 아래에 둔다 (m)")]
    public float extraMargin = 0.5f;

    // 수면을 출렁이게 하는 파도 4개의 (파장 × 가파름) 합. UnderwaterWaves.hlsl의 값과 같아야 함
    const float WaveAmplitudeSum = 16f * 0.012f + 10.5f * 0.011f + 7.2f * 0.010f + 5.3f * 0.009f;

    void LateUpdate()
    {
        var zone = DepthZoneSystem.Instance;
        if (zone == null || zone.depthSettings == null || zone.profile == null) return;

        // 파도 높이·파장 슬라이더를 바꿔도 따라가도록 매 프레임 계산
        float maxWave = WaveAmplitudeSum * zone.profile.waveLength * zone.profile.waveHeight;
        float limitY = zone.depthSettings.surfaceY - maxWave - extraMargin;

        Vector3 p = transform.position;
        if (p.y > limitY) transform.position = new Vector3(p.x, limitY, p.z);
    }
}
