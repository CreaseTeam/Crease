#ifndef PAPER_EDGE_SHADING_INCLUDED
#define PAPER_EDGE_SHADING_INCLUDED

TEXTURE2D_FLOAT(_EdgeSegmentTex);
int _EdgeSegmentCount;
float _EdgeDarkenWidth;
float _EdgeMinBrightness;

float4 LoadSegmentPixel(int segmentIndex, int pixelOffset)
{
    return LOAD_TEXTURE2D(_EdgeSegmentTex, int2(segmentIndex * 3 + pixelOffset, 0));
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

float EdgeBrightnessFromDistance(float distance)
{
    if (_EdgeDarkenWidth <= 0.0)
        return 1.0;

    float t = saturate(distance / _EdgeDarkenWidth);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(_EdgeMinBrightness, 1.0, t);
}

bool SegmentMatchesFace(float2 facePair, int faceId)
{
    if (facePair.x >= 0.0 && (int)facePair.x == faceId)
        return true;

    if (facePair.y >= 0.0 && (int)facePair.y == faceId)
        return true;

    return false;
}

float GetEdgeBrightness(float3 positionOS, float3 normalOS, float faceIndex)
{
    if (_EdgeSegmentCount <= 0 || _EdgeDarkenWidth <= 0.0)
        return 1.0;

    int faceId = (int)(faceIndex + 0.5);
    float3 n = normalize(normalOS);
    float minDistance = 1e6;

    for (int i = 0; i < _EdgeSegmentCount; i++)
    {
        float2 facePair = LoadSegmentPixel(i, 2).xy;
        if (!SegmentMatchesFace(facePair, faceId))
            continue;

        float3 a = LoadSegmentPixel(i, 0).xyz;
        float3 b = LoadSegmentPixel(i, 1).xyz;
        float3 planePos = positionOS - n * dot(positionOS - a, n);
        minDistance = min(minDistance, DistancePointToSegment(planePos, a, b));
    }

    if (minDistance > 1e5)
        return 1.0;

    return EdgeBrightnessFromDistance(minDistance);
}

#endif
