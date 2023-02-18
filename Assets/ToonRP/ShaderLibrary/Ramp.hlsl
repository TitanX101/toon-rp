﻿#ifndef TOON_RP_RAMP
#define TOON_RP_RAMP

#include "Math.hlsl"

float _ToonRP_GlobalRampEdge1;
float _ToonRP_GlobalRampEdge2;
float4 _ToonRP_GlobalShadowColor;

float ComputeRamp(const float nDotL, const float edge1, const float edge2)
{
    return smoothstep(edge1, edge2, nDotL);
}

float ComputeGlobalRamp(const float nDotL)
{
#ifdef _TOON_RP_GLOBAL_RAMP_CRISP
    return StepAntiAliased(_ToonRP_GlobalRampEdge1, nDotL);
#else // !_TOON_RP_GLOBAL_RAMP_CRISP
    return ComputeRamp(nDotL, _ToonRP_GlobalRampEdge1, _ToonRP_GlobalRampEdge2);
#endif // _TOON_RP_GLOBAL_RAMP_CRISP 
}

float3 MixShadowColor(const float3 albedo, const float4 shadowColor)
{
    return lerp(albedo, shadowColor.rgb, shadowColor.a);
}

float3 MixGlobalShadowColor(const float3 albedo)
{
    return MixShadowColor(albedo, _ToonRP_GlobalShadowColor);
}

float3 ApplyRamp(const float3 albedo, const float3 mixedShadowColor, const float ramp)
{
    return lerp(mixedShadowColor, albedo, ramp);
}

#endif // TOON_RP_RAMP
