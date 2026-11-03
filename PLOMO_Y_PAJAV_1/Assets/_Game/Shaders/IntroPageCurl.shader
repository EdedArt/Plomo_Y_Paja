// Pase de página del libro de la intro (A7 §4), estilo Cuphead. Shader de UI sin iluminación (RawImage en un Canvas
// Overlay; funciona en URP igual que UI/Default). _MainTex = página actual (la pone el RawImage), _NextTex = la siguiente.
//
// Modelo: rizo CILÍNDRICO. La línea del pliegue barre la página de derecha a izquierda (_Progress 0 → 1, el suavizado
// llega hecho desde C#), un poco inclinada para que la esquina de abajo a la derecha se levante primero. El papel a la
// derecha de la línea se enrolla sobre un cilindro de radio _Radius y lo que pasa de media vuelta queda plano encima,
// boca abajo (se ve el DORSO: hoja_papel en espejo y aclarada). Donde ya no hay papel se ve la página siguiente, con
// una sombra suave junto al pliegue. La cara del cilindro se sombrea según hacia dónde mira y lleva un brillo en el lomo.
// Todo en coordenadas de página con el alto = 1 y el ancho = _Aspect.
Shader "PlomoYPaja/Intro/PageCurl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Página actual (la pone el RawImage)", 2D) = "white" {}
        _NextTex ("Página siguiente", 2D) = "white" {}
        _PaperTex ("Papel del dorso (hoja_papel)", 2D) = "white" {}
        _Progress ("Progreso del pase", Range(0, 1)) = 0
        _Aspect ("Proporción ancho / alto", Float) = 1.7777778
        _Radius ("Radio del rizo (alto = 1)", Range(0.02, 0.4)) = 0.13
        _Angle ("Inclinación del pliegue (grados)", Range(-30, 30)) = 10
        _UnderTint ("Tinte del dorso (más claro)", Color) = (1.08, 1.05, 0.98, 1)
        _ShadowStrength ("Sombra sobre la página siguiente", Range(0, 1)) = 0.45
        _ShadowWidth ("Ancho de la sombra (alto = 1)", Range(0.005, 0.3)) = 0.09
        _FoldShade ("Oscurecido del cilindro de canto", Range(0, 1)) = 0.35
        _Highlight ("Brillo en el pliegue", Range(0, 1)) = 0.22
        _FlapShadow ("Sombra del dorso sobre la página actual", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PageCurl"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _NextTex;
            sampler2D _PaperTex;
            float _Progress;
            float _Aspect;
            float _Radius;
            float _Angle;
            float4 _UnderTint;
            float _ShadowStrength;
            float _ShadowWidth;
            float _FoldShade;
            float _Highlight;
            float _FlapShadow;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            static const float PI = 3.14159265;

            // ¿Este punto (coordenadas de página) cae dentro de la hoja?
            float Inside(float2 p)
            {
                return (p.x >= 0.0 && p.x <= _Aspect && p.y >= 0.0 && p.y <= 1.0) ? 1.0 : 0.0;
            }

            float2 ToUv(float2 p) { return float2(p.x / _Aspect, p.y); }

            float4 frag(v2f i) : SV_Target
            {
                // Derivadas fuera de las ramas: muestreo con mipmaps sin artefactos en los bordes de las ramas
                float2 dx = ddx(i.uv);
                float2 dy = ddy(i.uv);

                float2 p = float2(i.uv.x * _Aspect, i.uv.y);
                float a = radians(_Angle);
                float2 dir = normalize(float2(cos(a), -sin(a))); // del pliegue hacia el borde libre (derecha)
                float r = _Radius;

                // Recorrido de la línea: desde la esquina más alejada (nada rizado) hasta pasar la más cercana + el rizo
                float d0 = dot(float2(0, 0), dir);
                float d1 = dot(float2(_Aspect, 0), dir);
                float d2 = dot(float2(0, 1), dir);
                float d3 = dot(float2(_Aspect, 1), dir);
                float dMax = max(max(d0, d1), max(d2, d3));
                float dMin = min(min(d0, d1), min(d2, d3));
                float line0 = lerp(dMax + 0.001, dMin - r - _ShadowWidth, _Progress);

                float s = dot(p, dir) - line0; // distancia (con signo) a la línea del pliegue
                float fadeShadow = 1.0 - smoothstep(0.85, 1.0, _Progress);

                float4 next = tex2Dgrad(_NextTex, i.uv, dx, dy);
                float shadowNext = _ShadowStrength * fadeShadow * (1.0 - smoothstep(0.0, _ShadowWidth, s - r));
                float4 col = next * (1.0 - shadowNext);

                if (s < 0.0)
                {
                    // Detrás de la línea: o el trozo que ya dio media vuelta (dorso, plano encima), o la página actual
                    float u = PI * r - s;
                    float2 o = p + (u - s) * dir;
                    if (Inside(o) > 0.5)
                    {
                        float2 uvo = ToUv(o);
                        float4 paper = tex2Dgrad(_PaperTex, float2(1.0 - uvo.x, uvo.y), dx, dy) * _UnderTint;
                        col = float4(paper.rgb, 1.0);
                    }
                    else
                    {
                        float4 front = tex2Dgrad(_MainTex, i.uv, dx, dy);
                        // Sombra suave que el dorso echa sobre la página actual, justo pasando su borde
                        float2 flapPt = p + (PI * r - 2.0 * s) * dir; // ya fuera de la hoja: cuánto falta para volver
                        float edge = max(max(-flapPt.x, flapPt.x - _Aspect), max(-flapPt.y, flapPt.y - 1.0));
                        float flap = _FlapShadow * fadeShadow * (1.0 - smoothstep(0.0, _ShadowWidth * 0.6, edge))
                                     * step(0.0001, _Progress);
                        col = float4(front.rgb * (1.0 - flap), 1.0);
                    }
                }
                else if (s <= r)
                {
                    float th = asin(saturate(s / r));            // 0..π/2 (cara de abajo del cilindro)
                    float u2 = r * (PI - th);                    // cara de arriba: ya va boca abajo
                    float2 o2 = p + (u2 - s) * dir;
                    if (Inside(o2) > 0.5)
                    {
                        float nz = -cos(PI - th);                // 0 de canto … 1 mirando a cámara
                        float2 uvo = ToUv(o2);
                        float3 paper = tex2Dgrad(_PaperTex, float2(1.0 - uvo.x, uvo.y), dx, dy).rgb * _UnderTint.rgb;
                        float shade = lerp(1.0 - _FoldShade, 1.0, nz);
                        float hl = _Highlight * exp(-pow((nz - 0.55) / 0.2, 2.0));
                        col = float4(paper * shade + hl, 1.0);
                    }
                    else
                    {
                        float u1 = r * th;
                        float2 o1 = p + (u1 - s) * dir;
                        if (Inside(o1) > 0.5)
                        {
                            float nz = cos(th);
                            float3 front = tex2Dgrad(_MainTex, ToUv(o1), dx, dy).rgb;
                            col = float4(front * lerp(1.0 - _FoldShade, 1.0, nz), 1.0);
                        }
                        else
                        {
                            col = float4(next.rgb * (1.0 - _ShadowStrength * fadeShadow), 1.0);
                        }
                    }
                }

                col.a = 1.0;
                return col * i.color;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
