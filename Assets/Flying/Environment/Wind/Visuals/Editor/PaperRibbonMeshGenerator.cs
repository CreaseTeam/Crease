using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Crease.Flying.Environment.Wind.Visuals.EditorTools
{
    /// <summary>
    /// Builds the curved paper strip meshes used by the ambient ribbon VFX.
    ///
    /// Each mesh is a quad strip swept along a circular arc, tapered toward both tips.
    /// Geometry is single sided so the winding stays consistent, and the output shader
    /// renders it two sided.
    ///
    /// Every mesh is normalised to a maximum extent of one unit and recentred on its
    /// own bounds, so the VFX size attribute reads directly in metres and particles
    /// tumble about their visual centre rather than about an arbitrary corner.
    /// </summary>
    public static class PaperRibbonMeshGenerator
    {
        private const string OutputFolder = "Assets/Flying/Environment/Wind/Visuals/Meshes";

        /// <summary>
        /// Shape parameters for one ribbon variant.
        /// </summary>
        private struct RibbonShape
        {
            public string Name;

            /// <summary>Quads along the strip. Vertex count is (Segments + 1) * 2.</summary>
            public int Segments;

            /// <summary>Total arc the spine sweeps through, in degrees. 0 is a flat strip.</summary>
            public float SweepDegrees;

            /// <summary>Radius of the arc the spine follows. Larger is straighter.</summary>
            public float BendRadius;

            /// <summary>Half width of the strip at its midpoint, before taper.</summary>
            public float HalfWidth;

            /// <summary>0 is a rectangle, 0.35 a soft taper, 0.6 a crescent.</summary>
            public float TaperPower;

            /// <summary>Total twist about the spine from tip to tip, in degrees.</summary>
            public float TwistDegrees;
        }

        // Three silhouettes so a crowd of ribbons does not read as one repeated shape.
        // Kept to three because the VFX mesh output indexes a small fixed set.
        private static readonly RibbonShape[] Shapes =
        {
            new RibbonShape
            {
                Name = "RibbonCrescent",
                Segments = 10,
                SweepDegrees = 120f,
                BendRadius = 0.50f,
                HalfWidth = 0.16f,
                TaperPower = 0.60f,
                TwistDegrees = 25f
            },
            new RibbonShape
            {
                Name = "RibbonArc",
                Segments = 12,
                SweepDegrees = 55f,
                BendRadius = 1.10f,
                HalfWidth = 0.09f,
                TaperPower = 0.35f,
                TwistDegrees = 15f
            },
            new RibbonShape
            {
                Name = "RibbonCurl",
                Segments = 14,
                SweepDegrees = 200f,
                BendRadius = 0.35f,
                HalfWidth = 0.13f,
                TaperPower = 0.50f,
                TwistDegrees = 45f
            }
        };

        [MenuItem("Tools/Crease/Paper Ribbons/Generate Ribbon Meshes")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            var written = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (RibbonShape shape in Shapes)
                {
                    Mesh mesh = Build(shape);
                    string path = OutputFolder + "/" + shape.Name + ".asset";

                    // Rewriting in place would keep stale sub-assets around, so replace.
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }

                    AssetDatabase.CreateAsset(mesh, path);
                    written.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Generated " + written.Count + " paper ribbon meshes:\n  " + string.Join("\n  ", written));
        }

        private static Mesh Build(RibbonShape shape)
        {
            int segments = Mathf.Max(1, shape.Segments);
            int rings = segments + 1;

            var vertices = new Vector3[rings * 2];
            var uvs = new Vector2[rings * 2];
            var triangles = new int[segments * 6];

            float half = 0.5f * shape.SweepDegrees * Mathf.Deg2Rad;
            float cosHalf = Mathf.Cos(half);

            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / segments;   // 0 to 1 along the strip
                float s = t * 2f - 1f;           // -1 to 1, symmetric about the middle
                float theta = s * half;

                // Spine on a circular arc, shifted so the midpoint sits at the origin.
                var spine = new Vector3(
                    Mathf.Sin(theta),
                    Mathf.Cos(theta) - cosHalf,
                    0f) * shape.BendRadius;

                var tangent = new Vector3(Mathf.Cos(theta), -Mathf.Sin(theta), 0f);

                // Width runs perpendicular to the spine but inside the arc plane, which
                // is what makes the strip a flat crescent rather than a curved fence.
                var inPlaneNormal = new Vector3(Mathf.Sin(theta), Mathf.Cos(theta), 0f);

                // Twisting that width direction about the spine lifts the tips out of
                // the plane, so the strip catches light differently along its length and
                // never reads as a flat card.
                Vector3 widthDir = Quaternion.AngleAxis(shape.TwistDegrees * s, tangent) * inPlaneNormal;

                // Taper toward both tips. The floor keeps the tips from collapsing into
                // degenerate triangles, which render as flickering black slivers.
                float taper = Mathf.Pow(Mathf.Max(0f, 1f - s * s), shape.TaperPower);
                float halfWidth = Mathf.Max(shape.HalfWidth * taper, shape.HalfWidth * 0.06f);

                int v = i * 2;
                vertices[v] = spine + widthDir * halfWidth;
                vertices[v + 1] = spine - widthDir * halfWidth;
                uvs[v] = new Vector2(t, 0f);
                uvs[v + 1] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int tri = i * 6;

                triangles[tri] = v;
                triangles[tri + 1] = v + 2;
                triangles[tri + 2] = v + 1;

                triangles[tri + 3] = v + 1;
                triangles[tri + 4] = v + 2;
                triangles[tri + 5] = v + 3;
            }

            Normalise(vertices);

            var mesh = new Mesh { name = shape.Name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            return mesh;
        }

        /// <summary>
        /// Recentres on the bounds and scales so the largest extent is exactly one unit.
        /// </summary>
        private static void Normalise(Vector3[] vertices)
        {
            if (vertices.Length == 0) return;

            Vector3 min = vertices[0];
            Vector3 max = vertices[0];

            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            Vector3 centre = (min + max) * 0.5f;
            Vector3 size = max - min;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = largest > 1e-5f ? 1f / largest : 1f;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = (vertices[i] - centre) * scale;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
