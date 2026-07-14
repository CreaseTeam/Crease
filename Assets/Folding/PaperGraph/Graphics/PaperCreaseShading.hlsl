#ifndef PAPER_CREASE_SHADING_INCLUDED
#define PAPER_CREASE_SHADING_INCLUDED

TEXTURE2D_FLOAT(_FoldEdgeSegmentTex);
float _FoldEdgeSegmentCount;
float _FoldEdgeDarkenWidth;
float _FoldEdgeMinBrightness;

TEXTURE2D_FLOAT(_CreaseEdgeSegmentTex);
float _CreaseEdgeSegmentCount;
float _CreaseEdgeDarkenWidth;
float _CreaseEdgeMinBrightness;

float4 LoadEdgeSegmentPixel(Texture2D segmentTex, int segmentIndex, int pixelOffset)
{
    return LOAD_TEXTURE2D(segmentTex, int2(segmentIndex * 3 + pixelOffset, 0));
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

float3 ClosestPointOnSegment(float3 pos, float3 a, float3 b)
{
    float3 ab = b - a;
    float lengthSq = dot(ab, ab);
    if (lengthSq < 1e-10)
        return a;

    float t = saturate(dot(pos - a, ab) / lengthSq);
    return a + ab * t;
}

float BrightnessFromDistance(float distance, float width, float minBrightness)
{
    if (width <= 0.0)
        return 1.0;

    float t = saturate(distance / width);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(minBrightness, 1.0, t);
}

bool EdgeSegmentMatchesFace(float2 facePair, int faceId)
{
    if (facePair.x >= 0.0 && (int)facePair.x == faceId)
        return true;

    if (facePair.y >= 0.0 && (int)facePair.y == faceId)
        return true;

    return false;
}

float GetSegmentEdgeBrightness(
    float3 positionOS,
    float faceIndex,
    Texture2D segmentTex,
    float segmentCount,
    float darkenWidth,
    float minBrightness)
{
    if (segmentCount <= 0 || darkenWidth <= 0.0)
        return 1.0;

    int faceId = (int)(faceIndex + 0.5);
    float edgeDistance = 1e6;
    bool hasEdge = false;

    int count = (int)(segmentCount + 0.5);
    for (int i = 0; i < count; i++)
    {
        float4 faceData = LoadEdgeSegmentPixel(segmentTex, i, 2);
        if (!EdgeSegmentMatchesFace(faceData.xy, faceId))
            continue;

        float3 a = LoadEdgeSegmentPixel(segmentTex, i, 0).xyz;
        float3 b = LoadEdgeSegmentPixel(segmentTex, i, 1).xyz;
        edgeDistance = min(edgeDistance, DistancePointToSegment(positionOS, a, b));
        hasEdge = true;
    }

    if (!hasEdge)
        return 1.0;

    return BrightnessFromDistance(edgeDistance, darkenWidth, minBrightness);
}

float GetFoldEdgeBrightness(float3 positionOS, float faceIndex)
{
    return GetSegmentEdgeBrightness(
        positionOS,
        faceIndex,
        _FoldEdgeSegmentTex,
        _FoldEdgeSegmentCount,
        _FoldEdgeDarkenWidth,
        _FoldEdgeMinBrightness);
}

float GetCreaseEdgeBrightness(float3 positionOS, float faceIndex)
{
    return GetSegmentEdgeBrightness(
        positionOS,
        faceIndex,
        _CreaseEdgeSegmentTex,
        _CreaseEdgeSegmentCount,
        _CreaseEdgeDarkenWidth,
        _CreaseEdgeMinBrightness);
}

TEXTURE2D_FLOAT(_BoundaryEdgeSegmentTex);
float _BoundaryEdgeSegmentCount;
float _EdgeShadowDarkenWidth;
float _EdgeShadowInnerOffset;
float _EdgeShadowMinBrightness;

float4 LoadBoundaryEdgeSegmentPixel(int segmentIndex, int pixelOffset)
{
    return LOAD_TEXTURE2D(_BoundaryEdgeSegmentTex, int2(segmentIndex * 3 + pixelOffset, 0));
}

float GetEdgeShadowBrightness(float3 positionOS, float3 normalOS, float faceIndex)
{
    if (_BoundaryEdgeSegmentCount <= 0 || _EdgeShadowDarkenWidth <= 0.0)
        return 1.0;

    int faceId = (int)(faceIndex + 0.5);
    float3 receiverNormal = normalize(normalOS);
    float edgeDistance = 1e6;
    bool hasEdge = false;

    int segmentCount = (int)(_BoundaryEdgeSegmentCount + 0.5);
    for (int i = 0; i < segmentCount; i++)
    {
        float4 segmentData = LoadBoundaryEdgeSegmentPixel(i, 2);
        int ownerFaceId = (int)(segmentData.x + 0.5);
        if (ownerFaceId < 0 || ownerFaceId == faceId)
            continue;

        float3 shadowDir = segmentData.yzw;
        if (dot(shadowDir, shadowDir) < 1e-10)
            continue;

        float3 a = LoadBoundaryEdgeSegmentPixel(i, 0).xyz;
        float3 b = LoadBoundaryEdgeSegmentPixel(i, 1).xyz;
        float3 closest = ClosestPointOnSegment(positionOS, a, b);
        float3 toPixel = positionOS - closest;

        if (dot(toPixel, shadowDir) < 0.0)
            continue;

        // Only the paper side facing the edge receives its shadow. Requiring a
        // small separation also prevents coplanar front/back surfaces from both matching.
        if (dot(toPixel, receiverNormal) >= -1e-5)
            continue;

        float3 inPlaneOffset = toPixel - receiverNormal * dot(toPixel, receiverNormal);
        float dist = length(inPlaneOffset);
        if (dist < _EdgeShadowInnerOffset)
            continue;

        edgeDistance = min(edgeDistance, dist - _EdgeShadowInnerOffset);
        hasEdge = true;
    }

    if (!hasEdge)
        return 1.0;

    return BrightnessFromDistance(edgeDistance, _EdgeShadowDarkenWidth, _EdgeShadowMinBrightness);
}

#endif
