// Painted statues coloured by the scenery palette: the sprite's brightness, stretched from
// _LumMin.._LumMax to 0..1, picks a colour from the team's ramp. Drifting cloud shadows darken it,
// and a thin rim of moonlight catches the edges that face the light (up-left). The sprite colour
// still multiplies the result, so hit flashes, dimming and other tints keep working.
Shader "ArmyRoguelike/StatueRamp"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Ramp ("Team ramp", 2D) = "white" {}
        _LumMin ("Darkest brightness in the art", Float) = 0
        _LumMax ("Brightest brightness in the art", Float) = 1
        _RimColor ("Moonlight rim colour", Color) = (0, 0, 0, 0)
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
            sampler2D _Ramp;
            float _LumMin;
            float _LumMax;
            float4 _RimColor;

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
                float4 c = tex2D(_MainTex, i.uv);
                float3 rgb = c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                // Judge brightness as it looks (gamma), not in linear light, or dark stone crushes to black.
                rgb = LinearToGammaSpace(rgb);
                #endif
                float value = saturate((luma(rgb) - _LumMin) / max(_LumMax - _LumMin, 0.01));
                value *= 1 - cloudShadow(i.wpos);
                float3 col = tex2D(_Ramp, float2(value, 0.5)).rgb;

                // An edge lit by the moon: this pixel is solid but the one a few texels up-left is empty.
                float2 towardLight = float2(-1, 1) * _MainTex_TexelSize.xy * 3;
                float rim = c.a * (1 - tex2D(_MainTex, i.uv + towardLight).a);
                col = lerp(col, _RimColor.rgb, rim * _RimColor.a * (1 - cloudShadow(i.wpos)));
                return float4(col, c.a) * i.color;
            }
            ENDCG
        }
    }
}
