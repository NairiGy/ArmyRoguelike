// Scenery colouring: the texture's brightness (times the vertex colour's red channel) picks a colour
// from a palette ramp, dark to light. One greyscale backdrop gives a different mood per palette.
// Vertex green fades the result towards the brightness in vertex blue (haze), and _Scroll drifts
// the texture sideways (clouds). _Flow makes only the light parts of a painting (its clouds) swirl
// slowly, leaving darker shapes like a tower still.
Shader "ArmyRoguelike/PaletteRamp"
{
    Properties
    {
        _MainTex ("Greyscale", 2D) = "white" {}
        _Ramp ("Palette ramp", 2D) = "white" {}
        _PixelSize ("Pixelate (texels per block, 0 = off)", Float) = 0
        _Scroll ("Drift (UV per second)", Float) = 0
        _Flow ("Cloud swirl (UV amplitude, 0 = off)", Float) = 0
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            sampler2D _Ramp;
            float _PixelSize;
            float _Scroll;
            float _Flow;

            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + float2(_Time.y * _Scroll, 0);
                o.color = v.color;
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
                if (_Flow > 0)
                {
                    // Two slow noise fields push the lookup around, but only where the painting is light.
                    float t = _Time.y * 0.12;
                    float2 push = float2(noise(float3(uv * 5, t)), noise(float3(uv * 5 + 17.3, t))) - 0.5;
                    push += (float2(noise(float3(uv * 13, t * 1.7)), noise(float3(uv * 13 + 5.1, t * 1.7))) - 0.5) * 0.5;
                    float mask = smoothstep(0.5, 0.72, luma(tex2D(_MainTex, uv).rgb));
                    float2 moved = uv + push * _Flow * mask;
                    // Never pull dark pixels (the tower's edge) into the clouds.
                    uv = lerp(uv, moved, smoothstep(0.45, 0.65, luma(tex2D(_MainTex, moved).rgb)));
                }
                float4 c = tex2D(_MainTex, uv);
                float value = saturate(lerp(luma(c.rgb) * i.color.r, i.color.b, i.color.g));
                float3 col = tex2D(_Ramp, float2(value, 0.5)).rgb;
                return float4(col, c.a * i.color.a);
            }
            ENDCG
        }
    }
}
