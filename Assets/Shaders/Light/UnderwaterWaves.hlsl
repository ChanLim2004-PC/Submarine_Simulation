#ifndef UNDERWATER_WAVES_INCLUDED
#define UNDERWATER_WAVES_INCLUDED

// 수면, 커스틱스, 햇빛 줄기가 함께 쓰는 파도. 값은 DepthZoneSystem이 넘긴다

float4 _UW_WaveParams;     // x: 높이, y: 파장 배율, z: 속도, w: 마루 뾰족함
float4 _UW_RippleParams;   // x: 잔물결 세기, y: 잔물결 크기
float _UW_SurfaceY;

// 앞 4개는 격자를 움직이고, 짧은 뒤 4개는 노멀에만
#define UW_WAVE_COUNT 8
#define UW_GEOMETRY_WAVES 4
static const float UW_WaveAngle[UW_WAVE_COUNT] = { 0, 25, -30, 50, -55, 80, -15, 40 };                  // 바람 방향 기준 (도)
static const float UW_WaveLen[UW_WAVE_COUNT]   = { 16, 10.5, 7.2, 5.3, 3.4, 2.4, 1.6, 1.1 };             // 파장 (m)
static const float UW_WaveSteep[UW_WAVE_COUNT] = { 0.012, 0.011, 0.010, 0.009, 0.008, 0.007, 0.006, 0.005 };

void UW_WaveParams(int i, out float2 dir, out float k, out float amp, out float omega)
{
    float ang = radians(UW_WaveAngle[i] + 17.0);
    dir = float2(cos(ang), sin(ang));
    float lambda = UW_WaveLen[i] * _UW_WaveParams.y;
    k = TWO_PI / lambda;
    amp = lambda * UW_WaveSteep[i] * _UW_WaveParams.x;
    omega = sqrt(9.81 * k) * _UW_WaveParams.z;   // 깊은 물 분산 관계: 긴 파도일수록 빠르다
}

// Gerstner 파도 (GPU Gems 1, 1장). 마루가 뾰족하고 골이 넓어진다
float3 UW_GerstnerDisplacement(float2 xz, float t)
{
    float3 p = 0.0;
    [unroll] for (int i = 0; i < UW_GEOMETRY_WAVES; i++)
    {
        float2 d; float k, a, w;
        UW_WaveParams(i, d, k, a, w);
        float s, c;
        sincos(k * dot(d, xz) - w * t, s, c);
        p.xz += d * (_UW_WaveParams.w / (k * UW_GEOMETRY_WAVES)) * c;
        p.y += a * s;
    }
    return p;
}

// 위쪽 노멀. 픽셀보다 짧은 물결은 줄인다
// focus: 마루(빛을 모으는 볼록한 곳)일수록 1
float3 UW_WaveNormal(float2 xz, float t, float footprint, int waveCount, float rippleWeight, out float focus)
{
    float2 slope = 0.0;
    float lift = 0.0;
    float curv = 0.0, curvNorm = 0.0;

    [unroll] for (int i = 0; i < UW_WAVE_COUNT; i++)
    {
        if (i >= waveCount) break;
        float2 d; float k, a, w;
        UW_WaveParams(i, d, k, a, w);
        float fade = saturate(1.0 - footprint * 3.0 / (TWO_PI / k));
        float s, c;
        sincos(k * dot(d, xz) - w * t, s, c);
        slope += d * (k * a * c * fade);
        if (i < UW_GEOMETRY_WAVES) lift += (_UW_WaveParams.w / UW_GEOMETRY_WAVES) * s * fade;
        curv += a * k * k * s * fade;
        curvNorm += a * k * k;
    }

    if (rippleWeight > 0.0)
    {
        [unroll] for (int j = 0; j < 4; j++)
        {
            float ang = radians(17.0 + 47.0 * j);
            float2 d = float2(cos(ang), sin(ang));
            float lambda = (0.7 - 0.15 * j) * _UW_RippleParams.y;
            float k = TWO_PI / lambda;
            float a = lambda * 0.02 * _UW_RippleParams.x * rippleWeight;
            float fade = saturate(1.0 - footprint * 3.0 / lambda);
            float c = cos(k * dot(d, xz) - sqrt(9.81 * k) * _UW_WaveParams.z * t);
            slope += d * (k * a * c * fade);
        }
    }

    focus = curv / max(curvNorm, 1e-4);
    return normalize(float3(-slope.x, 1.0 - lift, -slope.y));
}

#endif
