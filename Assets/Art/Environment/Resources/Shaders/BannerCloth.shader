// A banner's cloth in the wind: pinned at the crossbar, moving more towards the hem. The texture holds its
// folds as grey around 0.5, which tint colours, so one painted cloth serves every banner.
Shader "ArmyRoguelike/BannerCloth"
{
    Properties
    {
        [PerRendererData] _MainTex ("Folds", 2D) = "grey" {}
        _Tint ("Cloth colour", Color) = (1, 1, 1, 1)
        _Phase ("Offset in the wind", Float) = 0
        _Top ("Where the cloth hangs free, from the image's top", Float) = 0.195
        _Hem ("Where the hem is, from the image's top", Float) = 0.8
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
            float4 _Tint;
            float _Phase;
            float _Top;
            float _Hem;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 wpos : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float down = 1 - i.uv.y;
                // 0 at the crossbar (pinned), 1 at the hem (moves most).
                float t = saturate((down - _Top) / (_Hem - _Top));
                float time = _Time.y + _Phase;
                float gust = 0.72 + 0.28 * sin(time * 0.47);

                // The whole cloth swings, with waves running down it.
                float swing = 0.7 * sin(time * 1.7 - t * 3.6) + 0.3 * sin(time * 2.9 - t * 8 + 1.3);
                float dx = 0.024 * pow(t, 1.6) * swing * gust;
                // The edges and hem ripple a little on their own.
                dx += 0.0045 * t * sin(i.uv.x * 26 + time * 2.3 - t * 6) * gust;
                float dy = 0.0035 * t * t * sin(i.uv.x * 19 + time * 2.1);

                float4 c = tex2D(_MainTex, i.uv - float2(dx, -dy));

                // Folds of light travel across the cloth, so it reads as fabric and not a sliding picture.
                float shimmer = 1 + 0.2 * pow(t, 0.7) * gust *
                    sin((i.uv.x - 0.5) * 30 + time * 1.9 - t * 4.5 + 1.4 * sin(t * 5 + time * 0.8));

                float3 col = _Tint.rgb * c.rgb * 2 * shimmer * i.color.rgb;
                // Light from nearby effects falls on the cloth too.
                float3 light = fxLight(i.wpos, 2);
                col = col * (1 + light * 3) + light * 0.03;
                return float4(col, c.a * i.color.a);
            }
            ENDCG
        }
    }
}
