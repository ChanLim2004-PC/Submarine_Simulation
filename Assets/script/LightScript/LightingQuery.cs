using System.Collections.Generic;
using UnityEngine;

// 탐조등 원뿔 하나를 값만으로 나타낸 것. Job(멀티스레드) 안에서도 쓸 수 있다
public struct SearchlightCone
{
    public Vector3 position;
    public Vector3 direction;   // 정규화된 방향
    public float cosOuter;      // 바깥 반각의 cos
    public float cosInner;      // 안쪽 반각의 cos
    public float range;
}

// 어떤 위치가 빛을 얼마나 받는지 계산한다 (0~1)
// 관측 품질의 "조명" 판정, 생물이 탐조등에 놀라는 반응에 쓴다
public static class LightingQuery
{
    // 관측에 충분한 밝기로 보는 기준
    public static float ObservationThreshold = 0.35f;

    // 가림 판정에 쓸 레이어. 0이면 가림을 무시한다 (지형 레이어가 정해지면 설정)
    public static LayerMask OcclusionMask = 0;

    static readonly List<SearchlightCone> coneBuffer = new List<SearchlightCone>();

    // 탐조등, 햇빛 중 가장 밝은 값
    public static float GetLightExposure(Vector3 worldPos)
    {
        float best = SunExposure(worldPos);

        GetSearchlightCones(coneBuffer);
        foreach (var cone in coneBuffer)
        {
            float e = ConeExposure(cone, worldPos);
            if (e <= best) continue;
            if (IsOccluded(cone.position, worldPos)) continue;
            best = e;
        }
        return best;
    }

    public static bool IsLitForObservation(Vector3 worldPos)
    {
        return GetLightExposure(worldPos) >= ObservationThreshold;
    }

    // 켜져 있는 탐조등 원뿔을 모두 담아 준다. 물고기 Job에 넘길 때 쓴다
    public static void GetSearchlightCones(List<SearchlightCone> result)
    {
        result.Clear();
        foreach (var sl in SearchlightController.Active)
        {
            if (!sl.IsOn || sl.Lights == null) continue;
            foreach (var l in sl.Lights)
            {
                if (l == null || !l.isActiveAndEnabled || l.type != LightType.Spot) continue;
                result.Add(new SearchlightCone
                {
                    position = l.transform.position,
                    direction = l.transform.forward,
                    cosOuter = Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad),
                    cosInner = Mathf.Cos(l.innerSpotAngle * 0.5f * Mathf.Deg2Rad),
                    range = l.range
                });
            }
        }
    }

    // 원뿔 하나의 노출값. 가운데·가까울수록 1, 원뿔 가장자리·사거리 끝으로 갈수록 0
    // 관리 객체를 쓰지 않아서 Burst Job 안에서도 호출할 수 있다
    public static float ConeExposure(in SearchlightCone cone, Vector3 worldPos)
    {
        Vector3 toTarget = worldPos - cone.position;
        float dist = toTarget.magnitude;
        if (dist >= cone.range) return 0f;
        if (dist < 0.0001f) return 1f;

        float cosAngle = Vector3.Dot(cone.direction, toTarget / dist);
        if (cosAngle <= cone.cosOuter) return 0f;

        float angular = Smooth(Mathf.InverseLerp(cone.cosOuter, cone.cosInner, cosAngle));
        float distance = 1f - Smooth(dist / cone.range);
        return angular * distance;
    }

    // 유광층에서만. 수면 1, 유광층 바닥(200m) 0
    static float SunExposure(Vector3 worldPos)
    {
        var sys = DepthZoneSystem.Instance;
        if (sys == null || sys.depthSettings == null || sys.profile == null) return 0f;
        float depth = sys.depthSettings.ToDisplayDepth(worldPos.y);
        return Mathf.Clamp01(1f - depth / Mathf.Max(1f, sys.profile.photicBottom));
    }

    static bool IsOccluded(Vector3 from, Vector3 to)
    {
        if (OcclusionMask == 0) return false;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        // 대상 자신에 맞지 않도록 조금 앞에서 멈춘다
        return Physics.Raycast(from, dir / dist, dist - 0.1f, OcclusionMask, QueryTriggerInteraction.Ignore);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
