// Filtro de película vieja del libro de la intro (A7 §5). Shader de UI procedural (sin texturas) para una Image a
// pantalla completa en un Canvas Overlay encima de todo. Mezcla "multiplicar ×2" (Blend DstColor SrcColor):
// resultado = 2 × salida × pantalla, así que salida 0.5 deja la imagen igual, menos la oscurece y más la aclara.
// Así se aplican grano, polvo, rayas, viñeta, tinte sepia y exposición SIN leer la pantalla (URP no tiene GrabPass).
// Los valores los manda OldFilmFilter.cs cada cuadro (_FilmFrame cambia a ~24 por segundo).
Shader "PlomoYPaja/Intro/OldFilm"
{
    Properties
    {
        [PerRendererData] _MainTex ("(sin uso)", 2D) = "white" {}
        _FilmFrame ("Cuadro de película", Float) = 0
        _Exposure ("Exposición", Float) = 1
        _Resolution ("Resolución de pantalla (px)", Vector) = (1920, 1080, 0, 0)
        _GrainStrength ("Grano", Range(0, 0.5)) = 0.16
        _GrainSize ("Tamaño del grano (px)", Range(1, 4)) = 1.6
        _DustChance ("Probabilidad de cada mota", Range(0, 1)) = 0.18
        _DustSize ("Radio de mota (alto = 1)", Range(0.001, 0.02)) = 0.006
        _DustStrength ("Fuerza del polvo", Range(0, 1)) = 0.7
        _ScratchChance ("Probabilidad de cada raya", Range(0, 1)) = 0.22
        _ScratchStrength ("Fuerza de las rayas", Range(0, 1)) = 0.35
        _ScratchWidth ("Ancho de raya (ancho = 1)", Range(0.0002, 0.005)) = 0.0012
        _ScratchHold ("Cuadros que dura un tramo de raya", Range(1, 48)) = 9
        _VignetteStrength ("Viñeta", Range(0, 1)) = 0.55
        _VignetteRadius ("Radio de la viñeta", Range(0.2, 1.2)) = 0.85
        _VignetteSoftness ("Suavidad de la viñeta", Range(0.05, 1)) = 0.55
        _Tint ("Tinte sepia", Color) = (1.04, 0.97, 0.84, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend DstColor SrcColor

        Pass
        {
            Name "OldFilm"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _FilmFrame;
            float _Exposure;
            float4 _Resolution;
            float _GrainStrength;
            float _GrainSize;
            float _DustChance;
            float _DustSize;
            float _DustStrength;
            float _ScratchChance;
            float _ScratchStrength;
            float _ScratchWidth;
            float _ScratchHold;
            float _VignetteStrength;
            float _VignetteRadius;
            float _VignetteSoftness;
            float4 _Tint;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Hash sin seno (Dave Hoskins): estable en cualquier GPU
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 Hash32(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yzz) * p3.zyx);
            }

            float ValueNoise(float x, float seed)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash12(float2(i, seed)), Hash12(float2(i + 1.0, seed)), f);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float frame = _FilmFrame;
                float aspect = _Resolution.x / max(1.0, _Resolution.y);
                float factor = 1.0;

                // Grano: ruido por "grano" de pantalla, nuevo en cada cuadro de película (dos tamaños mezclados)
                float2 px = floor(uv * _Resolution.xy / _GrainSize);
                float g1 = Hash12(px + frame * 37.17);
                float g2 = Hash12(floor(px * 0.5) + frame * 11.31 + 91.7);
                float grain = (g1 * 0.65 + g2 * 0.35) - 0.5;
                factor *= 1.0 + grain * 2.0 * _GrainStrength;

                // Polvo: hasta 8 motas por cuadro, en sitios al azar; la mayoría oscuras, algunas claras
                float dust = 0.0;
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    float3 h = Hash32(float2(frame * 1.37 + k * 7.91, k * 3.17 + 5.0));
                    if (h.z < _DustChance)
                    {
                        float3 h2 = Hash32(float2(k * 13.7 + 1.3, frame * 0.73 + 2.9));
                        float2 c = h.xy;
                        float radius = _DustSize * lerp(0.35, 1.0, h2.x);
                        float2 dv = (uv - c) * float2(aspect, 1.0);
                        // Un poco irregular: estirada según el hash
                        dv.x *= lerp(0.6, 1.6, h2.y);
                        float m = 1.0 - smoothstep(radius * 0.4, radius, length(dv));
                        dust += m * (h2.z < 0.75 ? -1.0 : 0.6);
                    }
                }
                factor *= 1.0 + clamp(dust, -1.0, 1.0) * _DustStrength;

                // Rayas verticales: 3, cada una vive en tramos de _ScratchHold cuadros, temblando un poco de lado
                float scratch = 0.0;
                [unroll]
                for (int j = 0; j < 3; j++)
                {
                    float seg = floor((frame + j * 5.0) / _ScratchHold);
                    float3 h = Hash32(float2(seg * 2.31 + j * 17.3, j * 4.7 + 1.0));
                    if (h.z < _ScratchChance)
                    {
                        float x = h.x + sin(frame * 0.9 + j * 2.0) * 0.0015;
                        float w = _ScratchWidth * lerp(0.6, 1.4, h.y);
                        float lineMask = 1.0 - smoothstep(w * 0.3, w, abs(uv.x - x));
                        // No cubre todo el alto: se corta por tramos
                        float along = smoothstep(0.35, 0.6, ValueNoise(uv.y * 4.0 + seg * 3.1, j + 7.0));
                        scratch += lineMask * along * (h.y < 0.6 ? 1.0 : -1.0);
                    }
                }
                factor *= 1.0 + clamp(scratch, -1.0, 1.0) * _ScratchStrength;

                // Viñeta oscura (algo ovalada para 16:9)
                float2 v = (uv - 0.5) * float2(aspect * 0.75, 1.0) * 2.0;
                float vig = smoothstep(_VignetteRadius, _VignetteRadius - _VignetteSoftness, length(v) * 0.6);
                factor *= lerp(1.0 - _VignetteStrength, 1.0, vig);

                float3 f = factor * _Exposure * _Tint.rgb;
                return float4(saturate(f * 0.5), 1.0);
            }
            ENDCG
        }
    }
}
