// Art experiment: lights a 3D statue for baking into a sprite (ArtExperiment.cs). Drawn with a
// command buffer outside the render pipeline, so it does its own simple painted-style lighting.
Shader "Hidden/ArtExperiment/StatueBake"
{
    Properties
    {
        _MainTex ("Base colour", 2D) = "white" {}
        _Color ("Stone colour", Color) = (1, 1, 1, 1)
        _UseTex ("Use texture", Float) = 0
        _Mottle ("Stone mottling", Float) = 0.12
    }
    SubShader
    {
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _UseTex;
            float _Mottle;
            float3 _LightDir; // towards the light, world space
            float3 _ViewDir;  // towards the camera, world space

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float2 uv : TEXCOORD1; float3 wpos : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float4 frag(v2f i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normal) * (front ? 1 : -1);
                float3 albedo = _UseTex > 0.5 ? tex2D(_MainTex, i.uv).rgb : _Color.rgb;
                if (_UseTex < 0.5)
                {
                    // Two scales of soft blotches, like the painted sandstone in the 2D image.
                    float m = noise(i.wpos * 6) * 0.6 + noise(i.wpos * 18) * 0.4;
                    albedo *= 1 + (m - 0.5) * 2 * _Mottle;
                }

                // Soft two-tone ramp: warm lit side, cool shadow side, as in hand-painted art.
                float ndl = dot(n, normalize(_LightDir));
                float lit = smoothstep(-0.05, 0.4, ndl);
                float3 shadowTint = float3(0.52, 0.5, 0.62);
                float3 light = lerp(shadowTint, float3(1.08, 1.03, 0.95), lit);
                // A thin rim so the silhouette separates from the board.
                float rim = pow(1 - saturate(dot(n, normalize(_ViewDir))), 3) * 0.25;
                return float4(albedo * light + rim, 1);
            }
            ENDCG
        }
    }
}
