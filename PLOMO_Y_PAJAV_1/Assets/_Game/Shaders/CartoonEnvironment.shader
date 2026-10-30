// Escenario "pintado" (fase 3A2, referencia Mouse P.I.): suave, sin contornos, la luz hace el trabajo.
// - Iluminación en 3 bandas suaves (luz principal + luces adicionales: los faroles son puntuales).
// - Las sombras tiran a violeta (_ShadowColor), nunca a negro.
// - Variación "pintada" procedural: manchas de color de baja frecuencia + grano de papel (_PaintNoise),
//   para que las superficies sin textura no parezcan plástico.
// - _BaseMap opcional con UV de mundo (_UseWorldUV): eje según la normal, 1 repetición ≈ 2 m.
// - Oscurecido cerca del piso (_FloorDarken) para dar peso a la base de paredes y muebles.
// - Recibe sombras y SSAO; pases ShadowCaster, DepthOnly y DepthNormals. Sin pase de contorno.
Shader "PlomoYPaja/CartoonEnvironment"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Textura (opcional)", 2D) = "white" {}
        [Toggle] _UseWorldUV ("UV de mundo", Float) = 0
        _ShadowColor ("Color de sombra", Color) = (0.42, 0.33, 0.62, 1)
        _BandSoftness ("Suavidad de las bandas", Range(0.01, 0.3)) = 0.08
        _PaintNoise ("Ruido pintado", Range(0, 1)) = 0.35
        _FloorDarken ("Oscurecer cerca del piso", Range(0, 1)) = 0.35
        _FloorLevel ("Altura del piso (m)", Float) = 0
        _FloorDarkenHeight ("Altura del oscurecido (m)", Float) = 0.6
        [HDR] _EmissionColor ("Emisión", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        // Orden de includes: Core -> CommonMaterial -> (Shadows / Lighting). Shadows.hlsl usa LerpWhiteTo, que vive en
        // CommonMaterial.hlsl; URP Lit lo recibe por LitInput/Lighting, y estos pases no, así que se incluye aquí.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _EmissionColor;
            half _UseWorldUV;
            half _BandSoftness;
            half _PaintNoise;
            half _FloorDarken;
            float _FloorLevel;
            float _FloorDarkenHeight;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardCartoon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // ---- Ruido barato (hash + value noise 3D) ----
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash13(i), n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0)), n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1)), n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1)), n111 = Hash13(i + float3(1, 1, 1));
                float x00 = lerp(n000, n100, f.x), x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x), x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // 3 niveles (0, 0.5, 1) con bordes suaves
            half Bands(half x)
            {
                half s = _BandSoftness;
                return 0.5 * smoothstep(0.08 - s, 0.08 + s, x) + 0.5 * smoothstep(0.5 - s, 0.5 + s, x);
            }

            half3 Albedo(Varyings i, float3 n)
            {
                float2 uv = i.uv;
                if (_UseWorldUV > 0.5)
                {
                    float3 a = abs(n);
                    float2 w = a.y > max(a.x, a.z) ? i.positionWS.xz : (a.x > a.z ? i.positionWS.zy : i.positionWS.xy);
                    uv = w * 0.5 * _BaseMap_ST.xy + _BaseMap_ST.zw; // 1 repetición ≈ 2 m
                }
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;

                // Manchas grandes de pintura + grano de papel
                float low = ValueNoise(i.positionWS * 0.45) * 0.65 + ValueNoise(i.positionWS * 1.3) * 0.35;
                float grain = Hash13(floor(i.positionWS * 60.0));
                c *= 1.0 + (low - 0.5) * 0.5 * _PaintNoise + (grain - 0.5) * 0.12 * _PaintNoise;

                // Base de paredes y muebles más oscura
                float h = saturate((i.positionWS.y - _FloorLevel) / max(0.01, _FloorDarkenHeight));
                c *= lerp(1.0 - _FloorDarken, 1.0, h * h * (3.0 - 2.0 * h));
                return c;
            }

            half3 LightTerm(Light light, float3 n)
            {
                half ndl = saturate(dot(n, light.direction));
                half lit = Bands(ndl * light.shadowAttenuation);
                return light.color * lit * light.distanceAttenuation;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 albedo = Albedo(i, n);

                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 direct = LightTerm(mainLight, n);

            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light light = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                    direct += LightTerm(light, n);
                }
            #endif

                // Ambiente teñido de violeta: lo que no ilumina ninguna luz queda violeta, no negro
                half3 ambient = SampleSH(n) * _ShadowColor.rgb * 1.6 + _ShadowColor.rgb * 0.04;
                // El AO se eleva al cuadrado en el ambiente: esquinas y bases claramente más oscuras (fase 3A3)
                half aoAmbient = ao.indirectAmbientOcclusion * ao.indirectAmbientOcclusion;
                half3 color = albedo * (ambient * aoAmbient + direct * ao.directAmbientOcclusion);
                color += _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragEmpty
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // Core y CommonMaterial ya vienen del HLSLINCLUDE; se repiten por claridad (tienen guardas)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vertShadow(Attributes v) : SV_POSITION
            {
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            half4 fragEmpty() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            float4 vertDepth(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half4 fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        // Necesario para el SSAO (fuente Depth Normals)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            VaryingsDN vertDN(Attributes v)
            {
                VaryingsDN o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 fragDN(VaryingsDN i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
