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

#endif
