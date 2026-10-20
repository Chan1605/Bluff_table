// 손패에서 지금 가리키는 카드 테두리 (로비 CharacterOutline과 같은 색).
// 카드는 납작해서 법선 방향으로 밀어내는 방식이 안 보이므로, 메쉬 중심(_CardCenter)에서 바깥쪽으로 넓힌 복사본을
// 카드 바로 뒤에 그린다. SeatHandCards가 카드 렌더러의 마지막 재질로 붙인다.
Shader "LiarsBar/CardOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.3, 1)
        _OutlineWidth ("Outline Width (World, meters)", Range(0, 0.03)) = 0.008
        _CardCenter ("Card Center (Object)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "CardOutline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Offset 2, 2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _CardCenter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(_CardCenter.xyz);
                float3 outward = positionWS - centerWS;
                float len = length(outward);
                if (len > 1e-5) positionWS += outward / len * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
