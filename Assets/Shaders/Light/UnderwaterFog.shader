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
            float4 _UW_CausticParams;    // x: 무늬 크기(m), y: 속도, z: 무늬가 사라지는 월드 깊이(m), w: 사라지는 거리(m)
            float4 _UW_ShaftParams;      // x: 햇빛 줄기 세기(0이면 꺼짐), y: 계산 거리(m), z: 줄기 간격(m)
            float4 _UW_SunColor;
            float4 _UW_DistortParams;    // x: 일렁임 세기(0이면 꺼짐), y: 촘촘함, z: 속도

            // 해시 상수는 Dave Hoskins "Hash without Sine" (Shadertoy, MIT)
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Worley 노이즈 F2 - F1. 셀 경계 쪽이 커스틱스 선이 된다
            float CellEdge(float2 p, float time)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float f1 = 8.0, f2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y);
                    float2 h = Hash22(cell + o);
                    float2 pt = o + 0.5 + 0.4 * sin(time + 6.2831 * h);
                    float dist = length(pt - f);
                    if (dist < f1) { f2 = f1; f1 = dist; }
                    else if (dist < f2) { f2 = dist; }
                }
                return f2 - f1;
            }

            // 멀리서 선이 픽셀보다 가늘어지면 깜빡여서 최소 굵기를 둔다
            float CausticLine(float2 p, float time)
            {
                float e = CellEdge(p, time);
                float w = max(0.12, fwidth(p.x) * 1.5);
                return pow(1.0 - smoothstep(0.0, w * 3.0, e), 3.0);
            }

            // 두 겹을 섞고, 채널마다 살짝 어긋나게 해서 색 번짐
            float3 Caustics(float2 p, float time)
            {
                p += 0.25 * sin(p.yx * 0.8 + time * 0.3);   // 격자 티 안 나게
                float2 q = p * 1.37 + 17.0;
                float3 c;
                c.r = CausticLine(p + 0.015, time) * 0.6 + CausticLine(q + 0.015, time * 1.3) * 0.4;
                c.g = CausticLine(p, time)         * 0.6 + CausticLine(q, time * 1.3)         * 0.4;
                c.b = CausticLine(p - 0.015, time) * 0.6 + CausticLine(q - 0.015, time * 1.3) * 0.4;
                return c * c;
            }

            // 햇빛 줄기용 (커스틱스보다 넓고 부드럽게)
            float ShaftPattern(float2 p, float time)
            {
                p += 0.25 * sin(p.yx * 0.8 + time * 0.3);
                float b = 1.0 - smoothstep(0.0, 0.45, CellEdge(p, time * 0.5));
                return b * b;
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

            // 시선 방향 레이마칭. 수면 무늬가 밝은 곳 아래에 있는 지점만 산란광을 더한다
            float3 SunShafts(float3 camPos, float3 dir, float maxDist, float2 pixel)
            {
                const int STEPS = 24;
                float3 L = normalize(_UW_SunDir.xyz);
                float invLy = 1.0 / max(L.y, 0.2);
                float stepLen = maxDist / STEPS;
                float jitter = InterleavedGradientNoise(pixel);
                float time = _Time.y * _UW_CausticParams.y;

                float3 sum = 0.0;
                [loop] for (int i = 0; i < STEPS; i++)
                {
                    float s = (i + jitter) * stepLen;
                    float3 p = camPos + dir * s;
                    if (p.y > 0.0) continue;   // 수면 위는 건너뜀

                    float beam = ShaftPattern((p.xz - p.y * L.xz * invLy) / _UW_ShaftParams.z, time);
                    float3 fromSurface = exp(-_UW_Sigma * (-p.y * invLy));   // 수면 → 여기
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

                    // 햇빛 방향으로 수면까지 올라간 위치 기준
                    float2 proj = worldPos.xz - worldPos.y * L.xz / max(L.y, 0.2);
                    float depthFade = saturate(1.0 + worldPos.y / max(_UW_CausticParams.z, 0.01));
                    float distFade = 1.0 - smoothstep(_UW_CausticParams.w * 0.5, _UW_CausticParams.w, d);

                    float3 c = Caustics(proj / max(_UW_CausticParams.x, 0.01), _Time.y * _UW_CausticParams.y);
                    src.rgb += src.rgb * c * (_UW_CausticStrength * facing * depthFade * distFade * 2.0);
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
