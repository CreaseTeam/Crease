#ifndef PAPER_CREASE_SHADING_INCLUDED
#define PAPER_CREASE_SHADING_INCLUDED

TEXTURE2D_FLOAT(_CreaseSegmentTex);
float _CreaseSegmentCount;
float _CreaseDarkenWidth;
float _CreaseMinBrightness;

float4 LoadCreaseSegmentPixel(int segmentIndex, int pixelOffset)
{
    return LOAD_TEXTURE2D(_CreaseSegmentTex, int2(segmentIndex * 3 + pixelOffset, 0));
}

float DistancePointToSegment(float3 pos, float3 a, float3 b)
{
    float3 ab = b - a;
    float lengthSq = dot(ab, ab);
    if (lengthSq < 1e-10)
        return distance(pos, a);

    float t = saturate(dot(pos - a, ab) / lengthSq);
    return distance(pos, a + ab * t);
}

float BrightnessFromDistance(float distance, float width, float minBrightness)
{
    if (width <= 0.0)
        return 1.0;

    float t = saturate(distance / width);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(minBrightness, 1.0, t);
}

bool CreaseSegmentMatchesFace(float2 facePair, int faceId)
{
    if (facePair.x >= 0.0 && (int)facePair.x == faceId)
        return true;

    if (facePair.y >= 0.0 && (int)facePair.y == faceId)
        return true;

    return false;
}

float GetCreaseBrightness(float3 positionOS, float faceIndex)
{
    if (_CreaseSegmentCount <= 0 || _CreaseDarkenWidth <= 0.0)
        return 1.0;

    int faceId = (int)(faceIndex + 0.5);
    float creaseDistance = 1e6;
    bool hasCrease = false;

    int segmentCount = (int)(_CreaseSegmentCount + 0.5);
    for (int i = 0; i < segmentCount; i++)
    {
        float4 faceData = LoadCreaseSegmentPixel(i, 2);
        if (!CreaseSegmentMatchesFace(faceData.xy, faceId))
            continue;

        float3 a = LoadCreaseSegmentPixel(i, 0).xyz;
        float3 b = LoadCreaseSegmentPixel(i, 1).xyz;
        creaseDistance = min(creaseDistance, DistancePointToSegment(positionOS, a, b));
        hasCrease = true;
    }

    if (!hasCrease)
        return 1.0;

    return BrightnessFromDistance(creaseDistance, _CreaseDarkenWidth, _CreaseMinBrightness);
}

#endif
