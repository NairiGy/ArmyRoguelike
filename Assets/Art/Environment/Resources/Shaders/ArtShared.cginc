// Shared by the scenery and statue shaders.
#ifndef ART_SHARED_INCLUDED
#define ART_SHARED_INCLUDED

float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

#endif
