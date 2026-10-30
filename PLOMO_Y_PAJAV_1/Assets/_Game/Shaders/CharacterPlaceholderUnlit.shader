// Placeholder de personaje (cápsulas) y objetos interactuables: color plano sin iluminación
// y contorno negro de tinta (casco invertido). Así la regla de contraste (personajes planos con
// contorno, escenario pintado sin contorno) se puede juzgar antes de tener el arte final.
// Fase 3A3: el grosor del contorno es en PÍXELES de pantalla (se empuja el casco en espacio de recorte),
// no en metros: con 2.5 cm a 8 m medía ~1 px y se perdía contra la noche. _Lift aclara el relleno.
Shader "PlomoYPaja/CharacterPlaceholderUnlit"
{
    Properties
    {
        _Tint ("Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Destello blanco", Range(0, 1)) = 0
        _OutlineColor ("Color del contorno", Color) = (0, 0, 0, 1)
        _OutlinePixels ("Grosor del contorno (px)", Range(0, 8)) = 3
        _Lift ("Aclarar el relleno", Range(0, 1)) = 0.3
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
            float _OutlinePixels;
            half _Lift;
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
                // Dirección del casco: mezcla normal + posición (las aristas de un cubo no se separan)
                float3 fromCenter = v.positionOS.xyz;
                float3 dirOS = v.normalOS + (dot(fromCenter, fromCenter) > 1e-6 ? normalize(fromCenter) : 0);
                float3 dirWS = normalize(TransformObjectToWorldDir(dirOS));

                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                // Grosor constante en píxeles: empuje en espacio de recorte, corregido por aspecto y por w
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;
                float2 dirPx = dirCS * _ScreenParams.xy;
                dirPx = dirPx / max(length(dirPx), 1e-5);
                posCS.xy += dirPx / _ScreenParams.xy * (2.0 * _OutlinePixels) * posCS.w;
                // Un poco hacia atrás para que nunca tape el relleno en el mismo píxel
                #if UNITY_REVERSED_Z
                posCS.z -= 0.0001 * posCS.w;
                #else
                posCS.z += 0.0001 * posCS.w;
                #endif
                o.positionCS = posCS;
                o.normalWS = dirWS;
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
                c = lerp(c, c * 0.5 + 0.5, _Lift); // más claro que el escenario: los personajes resaltan
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
