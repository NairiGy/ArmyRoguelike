// Moving water for a full-colour sea painting far below the tower. The painting is dragged along a flow
// (the wind, bent into swirls around rocks) in two copies half a cycle apart that cross-fade, so it flows
// on forever without drifting off; the brightest parts (foam, spray) catch a travelling sheen. _Mask keeps
// rocks and ships still (light = water, dark = solid; a soft painted mask, thresholded here).
Shader "ArmyRoguelike/Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _Mask ("Water mask (light = water)", 2D) = "white" {}
        _Distance ("Flow per cycle (texels)", Float) = 8
        _Speed ("Cycles per second", Float) = 0.25
        _Wind ("Wind direction", Vector) = (1, -0.35, 0, 0)
        _Glint ("Foam glint", Float) = 0.08
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
            float _Distance;
            float _Speed;
            float4 _Wind;
            float _Glint;
            // Up to four swirls: xy centre (uv), z radius (in image heights), w turn (+ anticlockwise).
            float4 _Swirls[4];

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

            float water(float2 uv) { return smoothstep(0.35, 0.6, tex2D(_Mask, uv).r); }

            float4 frag(v2f i) : SV_Target
            {
                // Work in image heights so swirls are round on screen whatever the painting's shape.
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 q = i.uv * float2(aspect, 1);

                // The flow here: the wind, turned into swirls near rocks, a little stronger or weaker in patches.
                float2 flow = normalize(_Wind.xy);
                for (int k = 0; k < 4; k++)
                {
                    float2 d = q - _Swirls[k].xy * float2(aspect, 1);
                    float r = length(d);
                    float f = saturate(1 - r / max(_Swirls[k].z, 0.0001));
                    flow += float2(-d.y, d.x) / max(r, 0.0001) * _Swirls[k].w * f * f;
                }
                flow *= lerp(0.6, 1.4, vnoise(q * 3));

                // Two copies half a cycle apart; each fades out as it resets. Patches are out of step, so
                // the resets never pulse across the whole sea at once.
                float t = _Time.y * _Speed + vnoise(q * 2.3) * 2;
                float phase0 = frac(t);
                float phase1 = frac(t + 0.5);
                float2 stride = flow * _Distance * _MainTex_TexelSize.xy * water(i.uv);
                float2 uv0 = i.uv - stride * phase0;
                float2 uv1 = i.uv - stride * phase1;
                float4 still = tex2D(_MainTex, i.uv);
                // Water next to a rock never picks up the rock: where a sample lands on something solid,
                // the pixel keeps its own colour.
                float4 c0 = lerp(still, tex2D(_MainTex, uv0), water(uv0));
                float4 c1 = lerp(still, tex2D(_MainTex, uv1), water(uv1));
                float4 c = lerp(c0, c1, abs(0.5 - phase0) * 2);

                // Foam and spray glint as a slow, large sheen passes over them.
                float sheen = vnoise(q * 1.5 - normalize(_Wind.xy) * _Time.y * 0.05 + 7.3);
                c.rgb += _Glint * water(i.uv) * smoothstep(0.55, 0.85, luma(c.rgb)) * smoothstep(0.45, 0.85, sheen);
                return c * i.color;
            }
            ENDCG
        }
    }
}
