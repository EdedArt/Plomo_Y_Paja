// Tarjetas del fondo (luna, nubes, colinas, silueta de Porcópolis): sin iluminación, teñidas hacia el color
// del horizonte según la distancia (_FogTint) en lugar de la niebla real, para que se lean como pintura.
// Mezcla configurable desde el material: recorte (colinas, ciudad) o transparencia (luna, nubes).
Shader "PlomoYPaja/BackdropCard"
{
    Properties
    {
        _MainTex ("Imagen", 2D) = "white" {}
        _Color ("Tinte", Color) = (1, 1, 1, 1)
        _HorizonColor ("Color del horizonte", Color) = (0.42, 0.29, 0.478, 1)
        _FogTint ("Mezcla con el horizonte", Range(0, 1)) = 0.2
        _Cutoff ("Corte de alpha", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Toggle] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "Card"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _HorizonColor;
                half _FogTint;
                half _Cutoff;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff - 0.001);
                c.rgb = lerp(c.rgb, _HorizonColor.rgb, _FogTint);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
