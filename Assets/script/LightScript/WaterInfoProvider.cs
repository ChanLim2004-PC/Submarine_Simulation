using UnityEngine;
using System.Collections.Generic;

// 수심에 따른 수압/수온/염분 정보 제공 - UI 쪽에서 이 값을 읽어서 계기판에 표시
// DepthSettings/UnderwaterProfile과 같은 수심 기준을 그대로 사용해 다른 파트와 값이 어긋나지 않게 함
public class WaterInfoProvider : MonoBehaviour
{
    public static WaterInfoProvider Instance { get; private set; }

    public DepthSettings depthSettings;
    public UnderwaterProfile profile;

    [Header("수온 (유광층 표층 → 무광층 하한)")]
    public float surfaceTemperatureC = 22f;
    public float deepTemperatureC = 2f;

    [Header("염분 - 기본값 + 브라인풀 근접 시 급상승")]
    public float baseSalinityPsu = 35f;
    public float brineSalinityPsu = 250f;
    public float brineInfluenceRadius = 15f;

    List<Transform> brinePools = new List<Transform>();

    public struct WaterInfo
    {
        public float depthMeters;
        public DepthZone zone;
        public float pressureAtm;
        public float temperatureC;
        public float salinityPsu;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameObject pools = GameObject.Find("SeabedBrinePools");
        if (pools != null)
            foreach (Transform t in pools.transform) brinePools.Add(t);
    }

    public WaterInfo GetInfo(Vector3 worldPos)
    {
        WaterInfo info = new WaterInfo();
        if (depthSettings == null || profile == null) return info;

        float depth = depthSettings.ToDisplayDepth(worldPos.y);
        info.depthMeters = depth;
        info.zone = depth < profile.photicBottom ? DepthZone.Photic
                  : depth < profile.twilightBottom ? DepthZone.Twilight
                  : DepthZone.Midnight;

        info.pressureAtm = 1f + depth / 10f; // 수심 10m마다 1기압 증가 (실제 해양 압력 근사치)

        float t = Mathf.InverseLerp(0f, profile.twilightBottom, depth);
        info.temperatureC = Mathf.Lerp(surfaceTemperatureC, deepTemperatureC, Mathf.Clamp01(t));

        info.salinityPsu = baseSalinityPsu + BrineBoost(worldPos);
        return info;
    }

    float BrineBoost(Vector3 worldPos)
    {
        float closest = float.MaxValue;
        for (int i = 0; i < brinePools.Count; i++)
        {
            if (brinePools[i] == null) continue;
            float d = Vector3.Distance(worldPos, brinePools[i].position);
            if (d < closest) closest = d;
        }
        if (closest >= brineInfluenceRadius) return 0f;
        float factor = 1f - closest / brineInfluenceRadius;
        return (brineSalinityPsu - baseSalinityPsu) * factor;
    }
}
