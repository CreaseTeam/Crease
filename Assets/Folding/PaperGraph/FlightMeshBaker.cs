using UnityEngine;

namespace Crease.Folding.Paper
{
    /// <summary>
    /// Bakes graph-space mesh geometry into the canonical flight frame (+Z = model front).
    /// </summary>
    public static class FlightMeshBaker
    {
        public static Mesh BakeFlightMesh(Mesh source, Quaternion orientation)
        {
            Mesh baked = Object.Instantiate(source);
            baked.name = source.name;

            if (orientation == Quaternion.identity)
                return baked;

            Vector3[] vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = orientation * vertices[i];
            baked.vertices = vertices;

            Vector3[] normals = baked.normals;
            if (normals != null && normals.Length == vertices.Length)
            {
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = orientation * normals[i];
                baked.normals = normals;
            }
            else
            {
                baked.RecalculateNormals();
            }

            baked.RecalculateBounds();
            return baked;
        }
    }
}
