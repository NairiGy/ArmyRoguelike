// A banner's cloth in the wind. It hangs from a slanted crossbar beside its post: pinned along the crossbar, and
// moving more towards the hem and away from the post. The texture holds its light and shade, which pick a colour
// from the banner's ramp (dark to light), as the statues do, so one painted cloth serves every banner.
Shader "ArmyRoguelike/BannerCloth"
{
    Properties
    {
        [PerRendererData] _MainTex ("Shade", 2D) = "grey" {}
        _Ramp ("Colour ramp", 2D) = "white" {}
        _Light ("Brightness in this scene", Float) = 1
        _Phase ("Offset in the wind", Float) = 0
        // The cloth's top edge in the image: the height from the image's top at _BarU across, and its slope.
        _BarDown ("Top edge height", Float) = 0.398
        _BarU ("Top edge reference", Float) = 0.28
        _BarSlope ("Top edge slope", Float) = -0.357
        _Length ("Cloth length", Float) = 0.31
        _PostU ("Post side of the cloth", Float) = 0.62
        _Width ("Cloth width", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "ArtShared.cginc"

        sampler2D _MainTex;
        sampler2D _Ramp;
        float _Light;
        float _Phase;
        float _BarDown;
        float _BarU;
        float _BarSlope;
        float _Length;
        float _PostU;
        float _Width;

        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

        float4 cloth(float2 uv, float4 color, float2 wpos)
        {
            float down = 1 - uv.y;
            float bar = _BarDown + (uv.x - _BarU) * _BarSlope;
            // 0 at the crossbar (pinned), 1 at the hem (moves most).
            float t = saturate((down - bar) / _Length);
            // 0 beside the post (moves least), 1 at the free edge.
            float free = saturate((_PostU - uv.x) / _Width);
            float time = _Time.y + _Phase;
            float gust = 0.72 + 0.28 * sin(time * 0.47);

            // The cloth swings, with waves running down it.
            float swing = 0.7 * sin(time * 1.7 - t * 3.6) + 0.3 * sin(time * 2.9 - t * 8 + 1.3);
            float dx = 0.03 * pow(t, 1.6) * lerp(0.3, 1, free) * swing * gust;
            // The edges and hem ripple a little on their own.
            dx += 0.004 * t * sin(uv.x * 26 + time * 2.3 - t * 6) * gust;
            // It moves in its own slanted plane: towards the post is also up the crossbar's slope.
            float dv = -_BarSlope * dx + 0.003 * t * t * sin(uv.x * 19 + time * 2.1);

            float4 c = tex2D(_MainTex, uv - float2(dx, dv));

            // Folds of light travel across the cloth, so it reads as fabric and not a sliding picture.
            float shimmer = 0.14 * pow(t, 0.7) * gust *
                sin((uv.x - 0.5) * 30 + time * 1.9 - t * 4.5 + 1.4 * sin(t * 5 + time * 0.8));

            // The middle of the ramp, so the painted folds stay folds and don't turn into stripes.
            float shade = lerp(0.25, 0.8, c.r);
            float3 col = tex2D(_Ramp, float2(saturate(shade + shimmer), 0.5)).rgb * _Light * color.rgb;
            // Light from nearby effects falls on the cloth too.
            float3 light = fxLight(wpos, 2);
            col = col * (1 + light * 3) + light * 0.03;
            return float4(col, c.a * color.a);
        }
        ENDCG

        // On the roof.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                return cloth(i.uv, i.color, i.wpos);
            }
            ENDCG
        }

        // On a screen's card (Graphics.DrawTexture in OnGUI), cut off at the edges of a scroll view. Its tag keeps
        // sprites on the roof from drawing this pass too.
        Pass
        {
            Tags { "LightMode" = "BannerGui" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _GUIClipTexture;
            uniform float4x4 unity_GUIClipTextureMatrix;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 clipUV : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                float3 eye = UnityObjectToViewPos(v.vertex);
                o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eye.xy, 0, 1)).xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = cloth(i.uv, i.color, float2(0, 0));
                c.a *= tex2D(_GUIClipTexture, i.clipUV).a;
                // Screens are drawn without the linear-to-sRGB step the roof gets, so do it here.
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
