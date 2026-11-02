// 수중 감쇠 — 화면 전체에 한 번 적용하는 Full Screen Pass용 셰이더
// 멀리 있는 픽셀일수록 RGB 채널별로 빛이 줄어 물색에 묻힌다 (빨강이 가장 빨리 사라짐)
//   t = exp(-σ · d),  최종색 = 원래색 × t + 물색 × (1 − t)
// σ(_UW_Sigma)와 물색(_UW_WaterColor)은 전역 셰이더 변수로 받는다 (수심대 시스템이 설정)
// _UW_Enabled가 1일 때만 동작한다 (기본 0 = 꺼짐)
Shader "Light/UnderwaterFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }   // URP 전용
        ZWrite Off      // 깊이 버퍼에 쓰지 않음 (화면 덮어쓰기만)
        ZTest Always    // 항상 그림
        Cull Off        // 면 방향 무시
        Blend Off       // 섞지 않고 결과로 교체

        Pass
        {
            Name "UnderwaterFog"

            HLSLPROGRAM
            #pragma vertex Vert       // 화면 전체 삼각형을 그리는 기본 버텍스 함수 (Blit.hlsl 제공)
            #pragma fragment Frag     // 아래에서 직접 만든 픽셀 함수

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"                 // URP 기본 함수·행렬
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"                  // _BlitTexture(원래 화면), Vert
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"  // SampleSceneDepth (깊이 버퍼 읽기)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float3 _UW_Sigma;        // 채널별 감쇠 계수 σ (R, G, B) /m — 전역 변수
            float4 _UW_WaterColor;   // 물색 — 전역 변수
            // 효과 켜짐 여부 — 전역 변수 (1 = 켜짐, 0 = 꺼짐)
            // 아무도 설정하지 않으면 기본값 0 → 효과 꺼짐 (팀원 씬에서는 원래 화면 그대로)
            // 켜고 끄는 쪽: UnderwaterFogPreview(테스트용), 이후 DepthZoneSystem(수심대 시스템)과 효과 토글 키
            float _UW_Enabled;

            // 커스틱스 (DepthZoneSystem이 설정, 0이면 꺼짐)
            float _UW_CausticStrength;   // 수면 1 → 유광층 바닥 0
            float4 _UW_SunDir;           // 해 쪽을 향하는 방향
            float4 _UW_CausticParams;    // z: 무늬가 사라지는 월드 깊이(m), w: 사라지는 거리(m)
            float4 _UW_ShaftParams;      // x: 햇빛 줄기 세기(0이면 꺼짐), y: 계산 거리(m)
            float4 _UW_SunColor;
            float4 _UW_DistortParams;    // x: 일렁임 세기(0이면 꺼짐), y: 촘촘함, z: 속도

            #include "UnderwaterWaves.hlsl"   // 수면 셰이더와 같은 파도

            // 수면 sxz에서 굴절된 햇빛이 h만큼 아래 수평면에 닿는 위치
            float2 RefractedHit(float2 sxz, float3 n, float3 L, float h, float eta)
            {
                float3 t = refract(-L, n, eta);
                return sxz + t.xz * (h / max(-t.y, 0.05));
            }

            // 굴절 면적비로 구한 빛 모임 (평평한 물 = 1)
            // Evan Wallace, "Rendering Realtime Caustics in WebGL" (2011) 방식을 픽셀 단위로
            float3 CausticFocus(float3 P, float3 L, float t, float footprint)
            {
                float h = max(_UW_SurfaceY - P.y, 0.0);
                float invLy = 1.0 / max(L.y, 0.2);
                // 해의 시직경(약 0.5°)만큼 깊을수록 번지게
                float e = max(max(0.03, footprint), h * 0.005);
                // 잔물결 무늬는 수면 근처에서만 (깊으면 지글거림)
                float ripple = saturate(1.0 - h / 4.0);
                float unused;

                // 이 점으로 오는 빛이 지난 수면 위치 (한 번 보정)
                float2 s = P.xz + L.xz * invLy * h;
                float3 n0 = UW_WaveNormal(s, t, footprint, UW_WAVE_COUNT, ripple, unused);
                s += P.xz - RefractedHit(s, n0, L, h, 1.0 / 1.333);

                float3 na = UW_WaveNormal(s, t, footprint, UW_WAVE_COUNT, ripple, unused);
                float3 nb = UW_WaveNormal(s + float2(e, 0.0), t, footprint, UW_WAVE_COUNT, ripple, unused);
                float3 nc = UW_WaveNormal(s + float2(0.0, e), t, footprint, UW_WAVE_COUNT, ripple, unused);

                // 채널별 굴절률 차이(분산)로 가장자리 색 번짐
                const float3 eta = 1.0 / float3(1.331, 1.333, 1.338);
                float3 I;
                [unroll] for (int ch = 0; ch < 3; ch++)
                {
                    float2 a = RefractedHit(s, na, L, h, eta[ch]);
                    float2 b = RefractedHit(s + float2(e, 0.0), nb, L, h, eta[ch]);
                    float2 c = RefractedHit(s + float2(0.0, e), nc, L, h, eta[ch]);
                    float det = ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) / (e * e);
                    I[ch] = 1.0 / max(abs(det), 0.15);
                }
                return I;
            }

            // 햇빛 줄기용. 큰 파도 4개만 써서 가볍게
            float ShaftFocus(float3 P, float3 L, float t)
            {
                float h = max(_UW_SurfaceY - P.y, 0.0);
                float invLy = 1.0 / max(L.y, 0.2);
                const float e = 0.25;
                const float eta = 1.0 / 1.333;
                float unused;
                float2 s = P.xz + L.xz * invLy * h;
                float3 na = UW_WaveNormal(s, t, 0.0, UW_GEOMETRY_WAVES, 0.0, unused);
                float3 nb = UW_WaveNormal(s + float2(e, 0.0), t, 0.0, UW_GEOMETRY_WAVES, 0.0, unused);
                float3 nc = UW_WaveNormal(s + float2(0.0, e), t, 0.0, UW_GEOMETRY_WAVES, 0.0, unused);
                float2 a = RefractedHit(s, na, L, h, eta);
                float2 b = RefractedHit(s + float2(e, 0.0), nb, L, h, eta);
                float2 c = RefractedHit(s + float2(0.0, e), nc, L, h, eta);
                float det = ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) / (e * e);
                return 1.0 / max(abs(det), 0.15);
            }

            // 레이마칭 줄무늬 방지용 지터. Jorge Jimenez, Interleaved Gradient Noise (SIGGRAPH 2014)
            float InterleavedGradientNoise(float2 pixel)
            {
                pixel += 5.588238 * fmod(floor(_Time.y * 60.0), 64.0);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            // Henyey-Greenstein (g = 0.6). 옆에서도 보이게 바닥값 0.25
            float ShaftPhase(float cosTheta)
            {
                const float g = 0.6;
                float hg = (1.0 - g * g) / pow(abs(1.0 + g * g - 2.0 * g * cosTheta), 1.5);
                return 0.25 + 0.75 * hg;
            }

            // 시선 방향 레이마칭. 빛이 모인 곳일수록 산란광이 많다
            float3 SunShafts(float3 camPos, float3 dir, float maxDist, float2 pixel)
            {
                const int STEPS = 24;
                float3 L = normalize(_UW_SunDir.xyz);
                float invLy = 1.0 / max(L.y, 0.2);
                float stepLen = maxDist / STEPS;
                float jitter = InterleavedGradientNoise(pixel);

                float3 sum = 0.0;
                [loop] for (int i = 0; i < STEPS; i++)
                {
                    float s = (i + jitter) * stepLen;
                    float3 p = camPos + dir * s;
                    if (p.y > _UW_SurfaceY) continue;   // 수면 위는 건너뜀

                    float beam = max(ShaftFocus(p, L, _Time.y) - 0.7, 0.0);
                    float3 fromSurface = exp(-_UW_Sigma * ((_UW_SurfaceY - p.y) * invLy));   // 수면 → 여기
                    float3 toCamera = exp(-_UW_Sigma * s);                    // 여기 → 카메라
                    sum += beam * fromSurface * toCamera;
                }
                return sum * stepLen * ShaftPhase(dot(dir, L)) * _UW_SunColor.rgb * _UW_ShaftParams.x * 0.06;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);   // VR 대응용 (일반 화면에서는 영향 없음)
                float2 uv = input.texcoord;                         // 이 픽셀의 화면 좌표 (0~1)

                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);   // 원래 화면 색

                if (_UW_Enabled < 0.5) return src;                  // 효과가 꺼져 있으면 원래 화면 그대로 반환

                // 일렁임 (Linear로 읽어야 계단이 안 생김)
                if (_UW_DistortParams.x > 0.0)
                {
                    float eye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    float amount = _UW_DistortParams.x * saturate(eye / 15.0);   // 가까운 건 거의 안 흔들린다
                    float2 sp = uv * _UW_DistortParams.y * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                    float tm = _Time.y * _UW_DistortParams.z;
                    float2 wobble;
                    wobble.x = sin(sp.y * 1.7 + tm) + 0.5 * sin(sp.x * 2.9 - tm * 1.3);
                    wobble.y = cos(sp.x * 1.9 + tm * 0.8) + 0.5 * cos(sp.y * 3.1 + tm * 1.1);
                    uv = clamp(uv + wobble * amount * 0.5, 0.001, 0.999);
                    src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                }

                float rawDepth = SampleSceneDepth(uv);              // 깊이 버퍼 값 (0~1, 플랫폼마다 방향이 다름)

                // 1) 지오메트리가 없는 픽셀(먼 평면)인지 판단
                #if UNITY_REVERSED_Z
                    bool isFar = rawDepth <= 1e-6;                  // DX11/Vulkan 등: 먼 곳 = 0
                #else
                    bool isFar = rawDepth >= 1.0 - 1e-6;            // OpenGL: 먼 곳 = 1
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);  // OpenGL은 -1~1 범위로 변환 필요
                #endif

                // 2) 깊이로 픽셀의 월드 위치를 복원하고 카메라까지 거리 d(m)를 구함
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);   // 화면좌표+깊이 → 월드 위치
                float d = distance(worldPos, GetCameraPositionWS());                             // 카메라~물체 거리

                // 커스틱스. 감쇠 전에 더해야 먼 무늬가 물색에 묻힌다
                // fwidth를 쓰므로 분기 조건은 전역 값만
                if (_UW_CausticStrength > 0.001)
                {
                    float3 L = normalize(_UW_SunDir.xyz);
                    float3 N = SampleSceneNormals(uv);
                    float facing = isFar ? 0.0 : saturate(dot(N, L));
                    float footprint = max(fwidth(worldPos.x), fwidth(worldPos.z));

                    float depthFade = saturate(1.0 + (worldPos.y - _UW_SurfaceY) / max(_UW_CausticParams.z, 0.01));
                    float distFade = 1.0 - smoothstep(_UW_CausticParams.w * 0.5, _UW_CausticParams.w, d);
                    float amount = _UW_CausticStrength * facing * depthFade * distFade;

                    // 모인 곳은 밝게, 퍼진 곳은 어둡게 (평균 밝기 유지)
                    float3 I = CausticFocus(worldPos, L, _Time.y, footprint);
                    src.rgb *= max(lerp(1.0, I, amount), 0.0);
                }

                // 3) 채널별 투과율 t = exp(-σ·d)
                float3 t = exp(-_UW_Sigma * d);
                t = isFar ? 0.0 : t;                                // 먼 평면은 전부 물색 (t = 0)

                // 4) 원래색과 물색을 투과율로 섞음
                float3 col = src.rgb * t + _UW_WaterColor.rgb * (1.0 - t);

                // 햇빛 줄기 (감쇠 뒤에 더하는 산란광)
                if (_UW_ShaftParams.x > 0.001)
                {
                    float3 camPos = GetCameraPositionWS();
                    float3 dir = normalize(worldPos - camPos);
                    float maxDist = isFar ? _UW_ShaftParams.y : min(d, _UW_ShaftParams.y);
                    col += SunShafts(camPos, dir, maxDist, input.positionCS.xy);
                }

                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
}
