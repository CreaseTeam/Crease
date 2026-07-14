#ifndef PAPER_EDGE_SHADING_INCLUDED
#define PAPER_EDGE_SHADING_INCLUDED

#define PAPER_SEGMENT_BOUNDARY 0.0
#define PAPER_SEGMENT_CREASE 1.0

TEXTURE2D_FLOAT(_EdgeSegmentTex);
int _EdgeSegmentCount;
float _BoundaryDarkenWidth;
float _BoundaryMinBrightness;
float _CreaseDarkenWidth;
float _CreaseMinBrightness;

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

float BrightnessFromDistance(float distance, float width, float minBrightness)
{
    if (width <= 0.0)
        return 1.0;

    float t = saturate(distance / width);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(minBrightness, 1.0, t);
}

bool SegmentMatchesFace(float2 facePair, int faceId)
{
    if (facePair.x >= 0.0 && (int)facePair.x == faceId)
        return true;

    if (facePair.y >= 0.0 && (int)facePair.y == faceId)
        return true;

    return false;
}

float GetCombinedEdgeBrightness(float3 positionOS, float3 normalOS, float faceIndex)
{
    if (_EdgeSegmentCount <= 0)
        return 1.0;

    int faceId = (int)(faceIndex + 0.5);
    float3 n = normalize(normalOS);
    float boundaryDistance = 1e6;
    float creaseDistance = 1e6;
    bool hasBoundary = false;
    bool hasCrease = false;

    for (int i = 0; i < _EdgeSegmentCount; i++)
    {
        float4 faceData = LoadSegmentPixel(i, 2);
        float2 facePair = faceData.xy;
        float segmentType = faceData.z;

        if (!SegmentMatchesFace(facePair, faceId))
            continue;

        float3 a = LoadSegmentPixel(i, 0).xyz;
        float3 b = LoadSegmentPixel(i, 1).xyz;

        if (segmentType < 0.5)
        {
            float3 planePos = positionOS - n * dot(positionOS - a, n);
            boundaryDistance = min(boundaryDistance, DistancePointToSegment(planePos, a, b));
            hasBoundary = true;
        }
        else
        {
            creaseDistance = min(creaseDistance, DistancePointToSegment(positionOS, a, b));
            hasCrease = true;
        }
    }

    float brightness = 1.0;

    if (hasBoundary && _BoundaryDarkenWidth > 0.0)
        brightness *= BrightnessFromDistance(boundaryDistance, _BoundaryDarkenWidth, _BoundaryMinBrightness);

    if (hasCrease && _CreaseDarkenWidth > 0.0)
        brightness *= BrightnessFromDistance(creaseDistance, _CreaseDarkenWidth, _CreaseMinBrightness);

    return brightness;
}

#endif
