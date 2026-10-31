// Moving water for a full-colour sea painting far below the tower. Each pixel samples the painting a
// little to the side, the shift driven by soft noise drifting with the wind, so the waves seem to heave;
// the brightest parts (foam, spray) catch a travelling sheen. _Mask keeps rocks and ships still (light =
// water, dark = solid; a soft painted mask, thresholded here).
Shader "ArmyRoguelike/Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _Mask ("Water mask (light = water)", 2D) = "white" {}
        _Strength ("Wobble (texels)", Float) = 2.5
        _Scale ("Wave size (noise cells across the height)", Float) = 9
        _Speed ("Speed", Float) = 0.12
        _Wind ("Wind direction", Vector) = (1, -0.35, 0, 0)
        _Glint ("Foam glint", Float) = 0.12
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
            float4 _MainTex_TexelSize;
            sampler2D _Mask;
            float _Strength;
            float _Scale;
            float _Speed;
            float4 _Wind;
            float _Glint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // Smooth value noise, 0..1.
            float vnoise(float2 p)
            {
                float2 c = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(c), hash21(c + float2(1, 0)), f.x),
                            lerp(hash21(c + float2(0, 1)), hash21(c + float2(1, 1)), f.x), f.y);
            }

            float4 frag(v2f i) : SV_Target
            {
                // Noise cells are square on screen whatever the painting's shape.
                float aspect = _MainTex_TexelSize.w / _MainTex_TexelSize.z;
                float2 p = i.uv * float2(_Scale / aspect, _Scale);
                float2 wind = normalize(_Wind.xy);
                float t = _Time.y * _Speed;
                float water = smoothstep(0.35, 0.6, tex2D(_Mask, i.uv).r);

                float2 shift = float2(vnoise(p - wind * t), vnoise(p * 1.7 - wind.yx * t * 0.8 + 3.1)) - 0.5;
                float2 moved = i.uv + shift * 2 * _Strength * _MainTex_TexelSize.xy * water;
                // Water next to a rock never picks up the rock: where the shifted sample lands on something
                // solid, the pixel keeps its own colour.
                float4 c = lerp(tex2D(_MainTex, i.uv), tex2D(_MainTex, moved), smoothstep(0.35, 0.6, tex2D(_Mask, moved).r));

                // Foam and spray glint as a slower, larger sheen passes over them.
                float sheen = vnoise(p * 0.6 - wind * t * 1.5 + 7.3);
                c.rgb += _Glint * water * smoothstep(0.55, 0.85, luma(c.rgb)) * smoothstep(0.45, 0.85, sheen);
                return c * i.color;
            }
            ENDCG
        }
    }
}
