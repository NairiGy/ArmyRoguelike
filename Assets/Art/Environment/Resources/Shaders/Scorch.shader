// A painted scorch mark (dark burn on a white background) laid on the floor. Two modes, as two
// materials on two sprites: _Mode 0 darkens the floor (multiply; the light stone in the painting is
// left out so only the burn shows), _Mode 1 adds the glow of hot embers in its darkest parts.
// Vertex alpha fades either one; _Heat (glow mode) is the ember colour's strength.
Shader "ArmyRoguelike/Scorch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Scorch", 2D) = "white" {}
        _Mode ("0 = burn, 1 = embers", Float) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 2
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
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
            float _Mode;

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
                float3 rgb = tex2D(_MainTex, i.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif
                float dark = 1 - luma(rgb);
                if (_Mode < 0.5)
                {
                    // Burn: how much of the painting to keep, from soot (some) to char (all).
                    float keep = smoothstep(0.3, 0.75, dark) * i.color.a;
                    float3 burn = lerp(1, rgb * 0.8, keep);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    burn = GammaToLinearSpace(burn);
                    #endif
                    return float4(burn, 1);
                }

                // Embers: the charred middle and the cracks glow, flickering.
                float hot = smoothstep(0.62, 0.95, dark);
                float flicker = 0.75 + 0.25 * sin(_Time.y * 11 + i.uv.x * 30) * sin(_Time.y * 7 + i.uv.y * 23);
                return float4(i.color.rgb * hot * flicker * i.color.a, 1);
            }
            ENDCG
        }
    }
}
