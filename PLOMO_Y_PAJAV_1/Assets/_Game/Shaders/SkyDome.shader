// Cúpula del cielo nocturno pintado: degradado (arriba #1B1F5C, horizonte #6B4A7A), estrellas procedurales
// que titilan, y opcionalmente una imagen (Backdrops/cielo_noche.png) con UV esféricas.
// Se dibuja primero (cola Background) sin escribir profundidad: todo lo demás queda delante.
Shader "PlomoYPaja/SkyDome"
{
    Properties
    {
        _TopColor ("Arriba", Color) = (0.106, 0.122, 0.361, 1)
        _HorizonColor ("Horizonte", Color) = (0.42, 0.29, 0.478, 1)
        _BottomColor ("Abajo", Color) = (0.16, 0.12, 0.2, 1)
        _MainTex ("Imagen (opcional)", 2D) = "white" {}
        [Toggle] _UseTexture ("Usar imagen", Float) = 0
        _StarDensity ("Densidad de estrellas", Range(0, 1)) = 0.35
        _StarBrightness ("Brillo de estrellas", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _UseTexture;
                half _StarDensity;
                half _StarBrightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirOS = v.positionOS.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dirOS);
                half up = saturate(d.y);
                half3 c = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, 0.55));
                c = d.y < 0 ? lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-d.y * 4)) : c;

                if (_UseTexture > 0.5)
                    c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;

                // Estrellas: celdas en coordenadas esféricas, más arriba = más estrellas
                float2 sph = float2(atan2(d.z, d.x), asin(d.y)) * 70.0;
                float2 cell = floor(sph);
                float2 f = frac(sph) - 0.5;
                float h = Hash21(cell);
                if (h > 1.0 - _StarDensity * 0.35 && d.y > 0.06)
                {
                    float2 off = float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) - 0.5;
                    float r = length(f - off * 0.6);
                    float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 3.0) + h * 40.0);
                    half star = smoothstep(0.13, 0.0, r) * twinkle * smoothstep(0.06, 0.3, d.y);
                    c += star * _StarBrightness * half3(1.0, 0.95, 0.85);
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
