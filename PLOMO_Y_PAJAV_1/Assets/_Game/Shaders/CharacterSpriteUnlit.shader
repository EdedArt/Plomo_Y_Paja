// Personajes 2D sin iluminación (estilo "Mouse P.I."): el sprite siempre se ve igual,
// sin importar las luces de la escena, y resalta sobre el fondo.
// - Alpha clip (corte 0.5): escribe profundidad, sin problemas de orden de transparencias.
// - No proyecta sombra real (la sombra es el "blob" que va bajo los pies) ni recibe niebla.
// - _FlashAmount: destello blanco al recibir un golpe. _Tint: tinte multiplicado.
Shader "PlomoYPaja/CharacterSpriteUnlit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Tint ("Tinte", Color) = (1, 1, 1, 1)
        _FlashAmount ("Destello blanco", Range(0, 1)) = 0
        _Cutoff ("Corte de alpha", Range(0, 1)) = 0.5
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Tint;
            half _FlashAmount;
            half _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color * _Tint;
            return o;
        }

        half4 SampleSprite(Varyings i)
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            clip(c.a - _Cutoff);
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SampleSprite(i);
                c.rgb = lerp(c.rgb, half3(1, 1, 1), _FlashAmount);
                return half4(c.rgb, 1);
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

            half4 fragDepth(Varyings i) : SV_Target
            {
                SampleSprite(i);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
