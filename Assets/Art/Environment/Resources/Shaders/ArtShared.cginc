// Shared by the scenery and statue shaders: noise, brightness, and the drifting cloud shadows that
// sweep across the whole scene (strength set globally by BattleScenery, 0 = off).
#ifndef ART_SHARED_INCLUDED
#define ART_SHARED_INCLUDED

float _CloudShadow;

float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

float noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

// How much a world position is in cloud shadow right now, 0..1: big soft blobs drifting slowly.
float cloudShadow(float2 wpos)
{
    float2 p = wpos * 0.28 + _Time.y * float2(0.05, 0.018);
    float n = noise(float3(p, 0)) * 0.65 + noise(float3(p * 2.3 + 7.1, 0)) * 0.35;
    return smoothstep(0.48, 0.68, n) * _CloudShadow;
}

#endif
