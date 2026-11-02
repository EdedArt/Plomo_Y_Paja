// Haz de luz falso bajo lámparas y faroles: cono abierto, aditivo, sin iluminación y muy tenue.
// Más brillante cerca de la bombilla (arriba), se apaga hacia el piso y en los bordes (efecto fresnel),
// y se desvanece si la cámara está muy cerca para no tapar la vista al atravesarlo.
Shader "PlomoYPaja/LightCone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.7, 0.4, 1)
        _Intensity ("Intensidad", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Cone"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float v : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.v = i.uv.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 toCam = _WorldSpaceCameraPos - i.positionWS;
                float dist = length(toCam);
                float3 viewDir = toCam / max(dist, 0.0001);
                half facing = abs(dot(normalize(i.normalWS), viewDir));
                half edge = pow(facing, 1.5);
                half height = pow(saturate(i.v), 1.6);
                half nearFade = saturate((dist - 0.6) / 1.5);
                half a = edge * height * nearFade * _Intensity;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
