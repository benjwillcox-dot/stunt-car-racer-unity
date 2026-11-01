using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StuntCar
{
    /// <summary>
    /// Accumulates vertices and triangles for a procedural mesh with several submeshes.
    /// Faces never share vertices, so RecalculateNormals gives flat, hard-edged shading.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> submeshes = new List<List<int>>();

        public MeshBuilder(int submeshCount)
        {
            for (int i = 0; i < submeshCount; i++) submeshes.Add(new List<int>());
        }

        /// <summary>Adds a quad. Corners must be clockwise when seen from the visible side.</summary>
        public void AddQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
            var t = submeshes[sub];
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void AddQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(sub, a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        // Face normal, then two in-plane axes u, v with Cross(v, u) == normal so the
        // corner order (-u-v, -u+v, +u+v, +u-v) is clockwise from outside.
        static readonly Vector3[,] CubeFaces =
        {
            { Vector3.right,   Vector3.forward, Vector3.up },
            { Vector3.left,    Vector3.back,    Vector3.up },
            { Vector3.up,      Vector3.right,   Vector3.forward },
            { Vector3.down,    Vector3.left,    Vector3.forward },
            { Vector3.forward, Vector3.up,      Vector3.right },
            { Vector3.back,    Vector3.down,    Vector3.right },
        };

        /// <summary>Adds a box transformed by the given matrix (unit cube centred on the origin).</summary>
        public void AddBox(int sub, Matrix4x4 m)
        {
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = CubeFaces[f, 0] * 0.5f, u = CubeFaces[f, 1] * 0.5f, v = CubeFaces[f, 2] * 0.5f;
                AddQuad(sub,
                    m.MultiplyPoint3x4(n - u - v),
                    m.MultiplyPoint3x4(n - u + v),
                    m.MultiplyPoint3x4(n + u + v),
                    m.MultiplyPoint3x4(n + u - v));
            }
        }

        // Face normal, then the texture's x and y axes on that face (Cross(y, x) == normal, so the
        // same corner order stays clockwise). Side faces keep the texture upright, unmirrored.
        static readonly Vector3[,] TexturedFaces =
        {
            { Vector3.right,   Vector3.forward, Vector3.up },
            { Vector3.left,    Vector3.back,    Vector3.up },
            { Vector3.up,      Vector3.right,   Vector3.forward },
            { Vector3.down,    Vector3.left,    Vector3.forward },
            { Vector3.forward, Vector3.left,    Vector3.up },
            { Vector3.back,    Vector3.right,   Vector3.up },
        };

        /// <summary>A box in one flat colour: every corner samples the same point (a palette cell).</summary>
        public void AddBox(int sub, Matrix4x4 m, Vector2 uv) => AddBox(sub, m, new Rect(uv, Vector2.zero));

        /// <summary>A box with each face showing the same rectangle of the texture, upright on the sides.</summary>
        public void AddBox(int sub, Matrix4x4 m, Rect uv)
        {
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = TexturedFaces[f, 0] * 0.5f, x = TexturedFaces[f, 1] * 0.5f, y = TexturedFaces[f, 2] * 0.5f;
                AddQuad(sub,
                    m.MultiplyPoint3x4(n - x - y), m.MultiplyPoint3x4(n - x + y), m.MultiplyPoint3x4(n + x + y), m.MultiplyPoint3x4(n + x - y),
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));
            }
        }

        /// <summary>A flat-coloured beam from a to b (see <see cref="AddBeam(int, Vector3, Vector3, float)"/>).</summary>
        public void AddBeam(int sub, Vector3 a, Vector3 b, float thickness, Vector2 uv)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 0.01f) return;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(axis / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            AddBox(sub, Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(axis, upHint), new Vector3(thickness, thickness, len)), uv);
        }

        /// <summary>Adds a square-section beam running from a to b.</summary>
        public void AddBeam(int sub, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 0.01f) return;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(axis / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var m = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(axis, upHint), new Vector3(thickness, thickness, len));
            AddBox(sub, m);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
