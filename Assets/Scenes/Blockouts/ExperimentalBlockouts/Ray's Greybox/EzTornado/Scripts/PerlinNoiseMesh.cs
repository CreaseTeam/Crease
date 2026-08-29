using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Scenes.Blockouts.RaysGreybox.EzTornado
{
    public class PerlinNoiseMesh : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)]
        [FormerlySerializedAs("m_threshold")]
        private float _threshold = 0.5f;

        [SerializeField, Range(0.05f, 0.3f)]
        [FormerlySerializedAs("m_scale")]
        private float _scale = 0.1f;

        private int _size = 100;
        private float _heightScale = 5f;

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
        }

        private void Start()
        {
            HeightInfo[,] heightArr = new HeightInfo[_size, _size];
            for (int x = 0; x < _size; ++x)
            {
                for (int z = 0; z < _size; ++z)
                {
                    float noise = Mathf.PerlinNoise((float)x * _scale, (float)z * _scale);
                    if (noise < _threshold)
                    {
                        float thRate = (_threshold - noise) / _threshold;
                        heightArr[x, z].Height = thRate * _heightScale;
                        heightArr[x, z].VertCol = new Color(1f - thRate * 1.1f, 0.8f + thRate * 0.3f, 0.9f - thRate * 1.2f);
                    }
                    else
                    {
                        heightArr[x, z].Height = 0f;
                        heightArr[x, z].VertCol = new Color(0.3f, 0.5f, 0.9f);
                    }
                }
            }

            Mesh mesh = CreateHeightMesh(heightArr);
            _meshFilter.mesh = mesh;
            _meshCollider.sharedMesh = mesh;
        }

        [System.Serializable]
        public struct HeightInfo
        {
            public float Height;
            public Color VertCol;
        }

        public static Mesh CreateHeightMesh(HeightInfo[,] heightArr, bool isUnitPerGrid = true)
        {
            int divX = heightArr.GetLength(0) - 1;
            int divZ = heightArr.GetLength(1) - 1;

            int vertNum = (divX + 1) * (divZ + 1);
            int quadNum = divX * divZ;
            int[] triangles = new int[quadNum * 6];
            Vector3[] vertices = new Vector3[vertNum];
            Vector2[] uv = new Vector2[vertNum];
            Color[] colors = new Color[vertNum];
            Vector3[] normals = new Vector3[vertNum];
            Vector4[] tangents = new Vector4[vertNum];

            for (int zz = 0; zz < (divZ + 1); ++zz)
            {
                for (int xx = 0; xx < (divX + 1); ++xx)
                {
                    float height = heightArr[xx, zz].Height;
                    Vector2 uvPos = new Vector2((float)xx / divX, (float)zz / divZ);
                    vertices[zz * (divX + 1) + xx] = new Vector3(uvPos.x - 0.5f, height, uvPos.y - 0.5f);
                    if (isUnitPerGrid)
                    {
                        vertices[zz * (divX + 1) + xx] = Vector3.Scale(vertices[zz * (divX + 1) + xx], new Vector3(divX, 1f, divZ));
                    }
                    uv[zz * (divX + 1) + xx] = uvPos;
                    colors[zz * (divX + 1) + xx] = heightArr[xx, zz].VertCol * 0.1f;
                    normals[zz * (divX + 1) + xx] = new Vector3(0.0f, 1.0f, 0.0f);
                    tangents[zz * (divX + 1) + xx] = new Vector4(1.0f, 0.0f, 0.0f);
                    if ((xx < divX) && (zz < divZ))
                    {
                        int[] sw = { 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1 };
                        for (int ii = 0; ii < 6; ++ii)
                        {
                            triangles[(zz * divX + xx) * 6 + ii] = (zz + sw[ii * 2 + 1]) * (divX + 1) + (xx + sw[ii * 2 + 0]);
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.SetIndices(mesh.GetIndices(0), MeshTopology.Triangles, 0);
            return mesh;
        }
    }
}
