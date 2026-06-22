#ifndef PAPER_FOLD_GUIDE_SHADING_INCLUDED
#define PAPER_FOLD_GUIDE_SHADING_INCLUDED

float _GuideActive;
float4 _GuideSegmentA;
float4 _GuideSegmentB;
float _GuideLineWidth;
float _GuideFalloffPower;
float _GuideMinBrightness;
float4 _GuideLineColor;
float _GuideDashEnabled;
float _GuideDashLength;
float _GuideDashGap;
float _GuideDashOffset;

float DistancePointToSegmentWithParam(float3 pos, float3 a, float3 b, out float t)
{
    float3 ab = b - a;
    float lengthSq = dot(ab, ab);
    if (lengthSq < 1e-10)
    {
        t = 0.0;
        return distance(pos, a);
    }

    t = saturate(dot(pos - a, ab) / lengthSq);
    return distance(pos, a + ab * t);
}

float GetFoldGuideMask(float3 positionOS)
{
    if (_GuideActive < 0.5 || _GuideLineWidth <= 0.0)
        return 0.0;

    float3 a = _GuideSegmentA.xyz;
    float3 b = _GuideSegmentB.xyz;
    float3 ab = b - a;
    float segmentLength = length(ab);
    if (segmentLength < 1e-6)
        return 0.0;

    float t;
    float distanceToLine = DistancePointToSegmentWithParam(positionOS, a, b, t);

    if (_GuideDashEnabled > 0.5)
    {
        float along = t * segmentLength;
        float period = max(_GuideDashLength + _GuideDashGap, 1e-5);
        float dashPhase = fmod(along + _GuideDashOffset, period);
        if (dashPhase > _GuideDashLength)
            return 0.0;
    }

    float radialT = saturate(distanceToLine / _GuideLineWidth);
    float falloff = pow(1.0 - radialT, max(_GuideFalloffPower, 0.001));
    return falloff;
}

half3 ApplyFoldGuideTint(half3 albedo, float3 positionOS)
{
    float guideMask = GetFoldGuideMask(positionOS);
    if (guideMask <= 0.0)
        return albedo;

    half blend = (half)guideMask * _GuideLineColor.a;
    half3 darkened = albedo * (half)_GuideMinBrightness;
    half3 tinted = lerp(darkened, _GuideLineColor.rgb, blend);
    return lerp(albedo, tinted, (half)guideMask);
}

#endif
