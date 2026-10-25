// Scenery colouring: the texture's brightness (times the vertex colour's red channel) picks a colour
// from a palette ramp, dark to light. One greyscale backdrop gives a different mood per palette.
// Vertex green fades the result towards the brightness in vertex blue (haze), and _Scroll drifts
// the texture sideways (clouds). _Flow makes only the light parts of a painting (its clouds) drift
// with the wind, leaving darker shapes like a tower still.
Shader "ArmyRoguelike/PaletteRamp"
{
    Properties
    {
        _MainTex ("Greyscale", 2D) = "white" {}
        _Ramp ("Palette ramp", 2D) = "white" {}
        _PixelSize ("Pixelate (texels per block, 0 = off)", Float) = 0
        _Scroll ("Drift (UV per second)", Float) = 0
        _Flow ("Cloud drift (UV per 7 s cycle, 0 = off)", Float) = 0
        _Shadowed ("Takes drifting cloud shadows (0..1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ArtShared.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            sampler2D _Ramp;
            float _PixelSize;
            float _Scroll;
            float _Flow;
            float _Shadowed;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 wpos : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + float2(_Time.y * _Scroll, 0);
                o.color = v.color;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_PixelSize > 0)
                {
                    float2 block = _MainTex_TexelSize.xy * _PixelSize;
                    uv = (floor(uv / block) + 0.5) * block;
                }
                float4 c = tex2D(_MainTex, uv);
                if (_Flow > 0)
                {
                    // The painting's clouds drift steadily with the wind: two copies slide along it half a
                    // cycle apart and cross-fade, so the drift never visibly resets. Only light areas move;
                    // a copy that would pull dark pixels (the tower's edge) into the clouds is not used there.
                    float cycle = _Time.y / 7;
                    float2 wind = normalize(CloudWind) * _Flow;
                    float mask = smoothstep(0.5, 0.72, luma(c.rgb));
                    float p0 = frac(cycle);
                    float p1 = frac(cycle + 0.5);
                    float4 s0 = tex2D(_MainTex, uv - wind * p0 * mask);
                    float4 s1 = tex2D(_MainTex, uv - wind * p1 * mask);
                    s0 = lerp(c, s0, smoothstep(0.45, 0.65, luma(s0.rgb)));
                    s1 = lerp(c, s1, smoothstep(0.45, 0.65, luma(s1.rgb)));
                    c = lerp(s1, s0, 1 - abs(2 * p0 - 1));
                }
                float value = saturate(lerp(luma(c.rgb) * i.color.r, i.color.b, i.color.g));
                if (_Shadowed > 0)
                {
                    // Drifting cloud shadows darken solid things (stone), never the light clouds themselves.
                    float solid = 1 - smoothstep(0.5, 0.72, luma(c.rgb));
                    value *= 1 - cloudShadow(i.wpos) * solid * _Shadowed;
                }
                float3 col = tex2D(_Ramp, float2(value, 0.5)).rgb;
                return float4(col, c.a * i.color.a);
            }
            ENDCG
        }
    }
}
