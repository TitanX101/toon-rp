﻿#ifndef TOON_RP_MATH
#define TOON_RP_MATH

float InverseLerpUnclamped(const float a, const float b, const float v)
{
    return (v - a) / (b - a);
}

float InverseLerpClamped(const float a, const float b, const float v)
{
    return saturate(InverseLerpUnclamped(a, b, v));
}

// https://www.ronja-tutorials.com/post/046-fwidth/
float StepAntiAliased(const float edge, const float value)
{
    const float halfChange = fwidth(value) * 0.5f;
    return InverseLerpClamped(edge - halfChange, edge + halfChange, value);
}

#endif // TOON_RP_MATH