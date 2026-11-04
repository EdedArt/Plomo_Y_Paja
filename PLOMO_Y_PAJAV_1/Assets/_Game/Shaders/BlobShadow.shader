// Sombra "blob" bajo los personajes: elipse oscura y suave, sin iluminación, multiplicada
// sobre el piso. Se dibuja en un quad; la forma sale de las UV (no necesita textura).
Shader "PlomoYPaja/BlobShadow"
{
    Properties
    {
        _ShadowColor ("Color de la sombra", Color) = (0.25, 0.18, 0.35, 1)
        _Strength ("Intensidad", Range(0, 1)) = 0.75
        _Fade ("Desvanecido", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Blob"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Strength;
                half _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv * 2 - 1);
                half a = smoothstep(1.0, 0.25, d) * _Strength * _Fade;
                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, a), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
