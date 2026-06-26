namespace Crease.Handwritting.Editor
{
    public struct HandwrittenBakedGlyph
    {
        public char Character;
        public int AtlasX;
        public int AtlasY;
        public int InkMinX;
        public int InkMinY;
        public int InkMaxX;
        public int InkMaxY;
        public float Advance;
        public float BearingX;
    }
}
