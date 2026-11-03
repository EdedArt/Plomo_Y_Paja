// Ventanas del taxi de la intro (INTRO.2): vidrio oscuro y transparente con chorritos de lluvia que bajan.
// Procedural (sin texturas), sin iluminación, transparente. Pasada sin LightMode: URP la dibuja como SRPDefaultUnlit.
// Las columnas de agua se desplazan con _Time (no hace falta script) y se inclinan un poco hacia atrás con _Slant
// (el taxi anda).
Shader "PlomoYPaja/Intro/RainWindow"
{
    Properties
    {
        _GlassColor ("Vidrio", Color) = (0.08, 0.1, 0.16, 0.35)
        _StreakColor ("Agua", Color) = (0.7, 0.8, 0.95, 0.55)
        _Columns ("Columnas", Float) = 26
        _Speed ("Velocidad de caída", Float) = 0.9
        _Length ("Largo de los chorritos", Range(0.05, 1)) = 0.35
        _Density ("Densidad", Range(0, 1)) = 0.55
        _Slant ("Inclinación", Range(-1, 1)) = 0.25
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RainWindow"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _GlassColor;
            float4 _StreakColor;
            float _Columns;
            float _Speed;
            float _Length;
            float _Density;
            float _Slant;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x += uv.y * _Slant * 0.3;
                float col = floor(uv.x * _Columns);
                float h = Hash(col + 1.0);
                float local = frac(uv.x * _Columns) - 0.5;
                float width = lerp(0.08, 0.22, Hash(col + 7.0));
                float lineMask = 1.0 - smoothstep(width * 0.5, width, abs(local));
                // Cada columna baja a su ritmo; el chorrito se estira hacia arriba y se apaga
                float y = frac(uv.y + _Time.y * _Speed * lerp(0.6, 1.4, h) + h * 10.0);
                float streak = smoothstep(0.0, 0.03, y) * (1.0 - smoothstep(0.03, _Length, y));
                float on = step(1.0 - _Density, Hash(col + 3.0));
                float water = lineMask * streak * on;
                float4 c = _GlassColor;
                c.rgb = lerp(c.rgb, _StreakColor.rgb, water);
                c.a = saturate(_GlassColor.a + water * _StreakColor.a);
                return c;
            }
            ENDCG
        }
    }
}
