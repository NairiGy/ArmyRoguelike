// Painted statues coloured by the scenery palette: the sprite's brightness, stretched from
// _LumMin.._LumMax to 0..1, picks a colour from the team's ramp. The sprite colour still multiplies
// the result, so hit flashes, dimming and other tints keep working.
Shader "ArmyRoguelike/StatueRamp"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Ramp ("Team ramp", 2D) = "white" {}
        _LumMin ("Darkest brightness in the art", Float) = 0
        _LumMax ("Brightest brightness in the art", Float) = 1
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
            sampler2D _Ramp;
            float _LumMin;
            float _LumMax;

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
                float4 c = tex2D(_MainTex, i.uv);
                float3 rgb = c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                // Judge brightness as it looks (gamma), not in linear light, or dark stone crushes to black.
                rgb = LinearToGammaSpace(rgb);
                #endif
                float value = saturate((dot(rgb, float3(0.299, 0.587, 0.114)) - _LumMin) / max(_LumMax - _LumMin, 0.01));
                float3 col = tex2D(_Ramp, float2(value, 0.5)).rgb;
                return float4(col, c.a) * i.color;
            }
            ENDCG
        }
    }
}
