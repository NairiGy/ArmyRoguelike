// Shared by the scenery and statue shaders.
#ifndef ART_SHARED_INCLUDED
#define ART_SHARED_INCLUDED

float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float2 hash22(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

// Cracks spreading through stone as damage (0..1) grows: the edges of irregular cells, a cell's edges
// showing once the damage passes that cell's own threshold. Returns how much of a crack line is here.
float cracks(float2 p, float damage)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float d1 = 8;
    float d2 = 8;
    float id = 0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float2 g = float2(x, y);
        float2 r = g + hash22(cell + g) - f;
        float d = dot(r, r);
        if (d < d1)
        {
            d2 = d1;
            d1 = d;
            id = hash21(cell + g);
        }
        else if (d < d2)
            d2 = d;
    }

    float edge = sqrt(d2) - sqrt(d1);
    return (1 - smoothstep(0.02, 0.07, edge)) * step(id, damage * 1.2 - 0.05);
}

// Up to four short-lived lights from effects (a sun beam striking, say), set globally by FxLight:
// xy world position, z radius, w strength, and a colour each. Squash > 1 stretches the falloff
// sideways, so on the isometric floor the light spreads as a flat ellipse.
float4 _FxLights[4];
float4 _FxLightColors[4];

float3 fxLight(float2 wpos, float squash)
{
    float3 sum = 0;
    for (int k = 0; k < 4; k++)
    {
        float2 d = (wpos - _FxLights[k].xy) * float2(1, squash);
        float r = max(_FxLights[k].z, 0.001);
        float f = saturate(1 - dot(d, d) / (r * r));
        // Cubed, so the light is strong in the middle and falls away quickly.
        sum += _FxLightColors[k].rgb * _FxLights[k].w * f * f * f;
    }
    return sum;
}

#endif
