// Placeholder de personaje (cápsulas) y objetos interactuables: color plano sin iluminación
// y contorno negro de tinta (casco invertido). Así la regla de contraste (personajes planos con
// contorno, escenario pintado sin contorno) se puede juzgar antes de tener el arte final.
Shader "PlomoYPaja/CharacterPlaceholderUnlit"
{
    Properties
    {
        _Tint ("Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Destello blanco", Range(0, 1)) = 0
        _OutlineColor ("Color del contorno", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Grosor del contorno (m)", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _FlashAmount;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
        };
        ENDHLSL

        // Contorno: la malla inflada hacia afuera, solo caras traseras
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            Varyings vertOutline(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                posWS += nWS * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = nWS;
                return o;
            }

            half4 fragOutline(Varyings i) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        // Relleno plano. Un toque mínimo de forma (según la normal en pantalla) para que se lea el volumen
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half rim = saturate(dot(n, float3(0, 1, 0)) * 0.5 + 0.5);
                half3 c = _Tint.rgb * lerp(0.85, 1.05, rim);
                c = lerp(c, half3(1, 1, 1), _FlashAmount);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = 0;
                return o;
            }

            half4 fragDepth(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
