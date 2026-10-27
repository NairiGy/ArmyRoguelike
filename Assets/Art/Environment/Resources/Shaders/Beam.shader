// A shaft of light falling from the sky, drawn on a sprite: UV x runs across the beam, UV y from the
// floor (0) up into the sky (1). The sprite is a painted shaft of light (it tiles vertically), repeated
// _Tile times up the beam and scrolling down it; a narrow white-hot core is added in the middle.
// Additive. Vertex colour tints it and its alpha sets the strength; _Core sets how much of the width
// the hot core takes.
Shader "ArmyRoguelike/Beam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painted shaft", 2D) = "white" {}
        _Core ("Core width (0..1)", Float) = 0.25
        _Speed ("Scroll speed (tiles per second)", Float) = 0.8
        _Tile ("Repeats up the beam", Float) = 4
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
            #include "ArtShared.cginc"

            sampler2D _MainTex;
            float _Core;
            float _Speed;
            float _Tile;

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
                float x = abs(i.uv.x * 2 - 1);
                float core = exp(-x * x / max(_Core * _Core * 0.25, 0.0001));

                // The painted shaft, twice: scrolling down at two speeds (one mirrored) so the repeat
                // never lines up and the light keeps moving. Its brightness is used; the vertex colour
                // gives the hue, so the strike can turn it white-hot.
                float2 p1 = float2(i.uv.x, i.uv.y * _Tile + _Time.y * _Speed);
                float2 p2 = float2(1 - i.uv.x, i.uv.y * _Tile * 0.7 + _Time.y * _Speed * 1.6 + 0.37);
                float3 a = tex2D(_MainTex, p1).rgb;
                float3 b = tex2D(_MainTex, p2).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                a = LinearToGammaSpace(a);
                b = LinearToGammaSpace(b);
                #endif
                float paint = luma(a) * 0.65 + luma(b) * 0.55;

                // Fades into the sky at the top; meets the floor with a soft bright foot.
                float ends = smoothstep(1, 0.55, i.uv.y) * smoothstep(0, 0.015, i.uv.y);
                float foot = 1 + exp(-i.uv.y * 40) * 0.6;

                float3 col = (i.color.rgb * paint * 1.4 + lerp(i.color.rgb, 1, 0.8) * core * 0.8) * foot * ends;
                return float4(col, i.color.a);
            }
            ENDCG
        }
    }
}
