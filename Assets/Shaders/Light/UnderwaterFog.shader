// L1 수중 감쇠 — 화면 전체에 한 번 적용하는 Full Screen Pass용 셰이더
// 멀리 있는 픽셀일수록 RGB 채널별로 빛이 줄어 물색에 묻힌다 (빨강이 가장 빨리 사라짐)
//   t = exp(-σ · d),  최종색 = 원래색 × t + 물색 × (1 − t)
// σ(_UW_Sigma)와 물색(_UW_WaterColor)은 전역 셰이더 변수로 받는다 (L3 수심대 시스템이 설정)
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

            float3 _UW_Sigma;        // 채널별 감쇠 계수 σ (R, G, B) /m — 전역 변수
            float4 _UW_WaterColor;   // 물색 — 전역 변수
            // 효과 켜짐 여부 — 전역 변수 (1 = 켜짐, 0 = 꺼짐)
            // 아무도 설정하지 않으면 기본값 0 → 효과 꺼짐 (팀원 씬에서는 원래 화면 그대로)
            // 켜고 끄는 쪽: UnderwaterFogPreview(테스트용), 이후 DepthZoneSystem(L3)과 L9 효과 토글 키
            float _UW_Enabled;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);   // VR 대응용 (일반 화면에서는 영향 없음)
                float2 uv = input.texcoord;                         // 이 픽셀의 화면 좌표 (0~1)

                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);   // 원래 화면 색

                if (_UW_Enabled < 0.5) return src;                  // 효과가 꺼져 있으면 원래 화면 그대로 반환

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

                // 3) 채널별 투과율 t = exp(-σ·d)
                float3 t = exp(-_UW_Sigma * d);
                t = isFar ? 0.0 : t;                                // 먼 평면은 전부 물색 (t = 0)

                // 4) 원래색과 물색을 투과율로 섞음
                float3 col = src.rgb * t + _UW_WaterColor.rgb * (1.0 - t);
                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
}
