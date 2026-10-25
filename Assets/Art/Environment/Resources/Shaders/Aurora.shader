// Aurora curtains for the night sky: a wavy band of vertical light streaks that slowly sways and
// shimmers, teal at the bottom fading to violet at the top. Additive.
Shader "ArmyRoguelike/Aurora"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 0.5
        _Low ("Lower colour", Color) = (0.3, 1, 0.75, 1)
        _High ("Upper colour", Color) = (0.6, 0.4, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Intensity;
            float4 _Low;
            float4 _High;

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

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float x = i.uv.x * 5 + t * 0.03;
                // The curtain's lower edge waves slowly; light hangs above it and fades upwards.
                float edge = 0.25 + 0.12 * sin(x + 1.5 * sin(x * 0.6 + t * 0.2));
                float above = i.uv.y - edge;
                float curtain = smoothstep(-0.03, 0.04, above) * exp(-max(above, 0) * 3.5);
                // Fine vertical streaks that drift and shimmer.
                float streaks = 0.55 + 0.45 * sin(i.uv.x * 70 + 4 * sin(i.uv.x * 9 + t * 0.4) + t * 0.6);
                float fadeSides = smoothstep(0, 0.15, i.uv.x) * smoothstep(1, 0.85, i.uv.x);
                float a = curtain * streaks * fadeSides * _Intensity * i.color.a;
                float3 col = lerp(_Low.rgb, _High.rgb, saturate(above * 2.5));
                return float4(col, a);
            }
            ENDCG
        }
    }
}
