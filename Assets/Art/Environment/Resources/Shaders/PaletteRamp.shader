// Scenery colouring: the texture's brightness (times the vertex colour's red channel) picks a colour
// from a palette ramp, dark to light. One greyscale backdrop gives a different mood per palette.
// Vertex green fades the result towards the brightness in vertex blue (haze), and _Scroll drifts
// the texture sideways (clouds).
Shader "ArmyRoguelike/PaletteRamp"
{
    Properties
    {
        _MainTex ("Greyscale", 2D) = "white" {}
        _Ramp ("Palette ramp", 2D) = "white" {}
        _PixelSize ("Pixelate (texels per block, 0 = off)", Float) = 0
        _Scroll ("Drift (UV per second)", Float) = 0
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
                float4 c = tex2D(_MainTex, uv);
                float value = saturate(lerp(dot(c.rgb, float3(0.299, 0.587, 0.114)) * i.color.r, i.color.b, i.color.g));
                float3 col = tex2D(_Ramp, float2(value, 0.5)).rgb;
                return float4(col, c.a * i.color.a);
            }
            ENDCG
        }
    }
}
