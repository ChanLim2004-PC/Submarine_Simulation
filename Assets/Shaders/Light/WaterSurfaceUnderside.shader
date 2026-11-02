// 물속에서 올려다본 수면
// Gerstner 파도 + 물 → 공기 굴절. 스넬의 창 안은 하늘·해, 밖은 전반사, 경계는 프레넬
// 불투명이라 수중 감쇠가 그대로 걸린다. 파도는 UnderwaterWaves.hlsl
Shader "Light/WaterSurfaceUnderside"
{
    Properties
    {
        _SkyBoost ("창 안쪽 밝기", Range(0, 5)) = 1.5
        _SunGlint ("해 반짝임", Range(0, 50)) = 20
        _FocusStrength ("빛이 모인 띠", Range(0, 3)) = 0.8
        _ReflectionDarken ("창 바깥(반사) 밝기", Range(0, 2)) = 0.8

        [HideInInspector] _GridCell ("격자 간격 (m)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Front   // 아래에서 볼 때만 그린다

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"   // DecodeHDREnvironment
        #include "UnderwaterWaves.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _SkyBoost;
            float _SunGlint;
            float _FocusStrength;
            float _ReflectionDarken;
            float _GridCell;
        CBUFFER_END

        // DepthZoneSystem이 설정하는 전역 값
        float _UW_Enabled;
        float4 _UW_SunDir;
        float4 _UW_SunColor;
        float4 _UW_WaterColor;

        struct Attributes
        {
            float4 positionOS : POSITION;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 gridXZ : TEXCOORD1;   // 출렁이기 전 위치 (픽셀 노멀용)
        };

        // 카메라를 따라가되 격자 간격 단위로 옮겨서 파도가 미끄러지지 않게
        Varyings Vert(Attributes v)
        {
            float2 cam = GetCameraPositionWS().xz;
            float2 snap = floor(cam / _GridCell) * _GridCell;
            float2 xz = v.positionOS.xz + snap;
            float3 disp = UW_GerstnerDisplacement(xz, _Time.y);

            Varyings o;
            o.positionWS = float3(xz.x + disp.x, _UW_SurfaceY + disp.y, xz.y + disp.z);
            o.gridXZ = xz;
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
        }

        // 수중 효과가 꺼지면 수면도 숨긴다
        void ClipIfOff()
        {
            clip(_UW_Enabled - 0.5);
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // 유전체 프레넬 (물 → 공기). 임계각을 넘으면 전반사
            float FresnelWaterToAir(float cosI, float eta)
            {
                float sinT2 = eta * eta * (1.0 - cosI * cosI);
                if (sinT2 >= 1.0) return 1.0;
                float cosT = sqrt(1.0 - sinT2);
                float rs = (eta * cosI - cosT) / (eta * cosI + cosT);
                float rp = (cosI - eta * cosT) / (cosI + eta * cosT);
                return 0.5 * (rs * rs + rp * rp);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                ClipIfOff();

                const float eta = 1.33;
                float footprint = max(fwidth(i.gridXZ.x), fwidth(i.gridXZ.y));
                float focus;
                float3 N = UW_WaveNormal(i.gridXZ, _Time.y, footprint, UW_WAVE_COUNT, 1.0, focus);
                float3 V = normalize(i.positionWS - GetCameraPositionWS());   // 카메라 → 수면 (위쪽)
                float3 Nw = -N;                                                // 물 쪽을 향하는 노멀

                float cosI = saturate(dot(V, N));
                float F = FresnelWaterToAir(cosI, eta);

                // 창 안: 굴절 방향의 하늘과 해. 마루 아래는 더 밝게
                float3 sky = 0.0;
                if (F < 1.0)
                {
                    float3 R = normalize(refract(V, Nw, eta));
                    half4 enc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, 0);
                    sky = DecodeHDREnvironment(enc, unity_SpecCube0_HDR) * _SkyBoost;

                    float3 L = normalize(_UW_SunDir.xyz);
                    float s = saturate(dot(R, L));
                    sky += _UW_SunColor.rgb * (pow(s, 1500.0) * _SunGlint + pow(s, 12.0) * 0.5);
                    sky *= 1.0 + saturate(focus) * _FocusStrength;
                }

                // 창 밖: 전반사
                float3 reflection = _UW_WaterColor.rgb * _ReflectionDarken;

                float3 col = lerp(sky, reflection, F);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // 깊이에도 출렁이는 모양 그대로 (수중 감쇠가 거리를 쓰므로)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings i) : SV_Target
            {
                ClipIfOff();
                return 0;
            }
            ENDHLSL
        }

        // 깊이+노멀. 아래쪽 노멀이라 커스틱스가 수면엔 안 비친다
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 FragDepthNormals(Varyings i) : SV_Target
            {
                ClipIfOff();
                float3 normalWS = float3(0.0, -1.0, 0.0);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                return half4(PackFloat2To888(oct), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
