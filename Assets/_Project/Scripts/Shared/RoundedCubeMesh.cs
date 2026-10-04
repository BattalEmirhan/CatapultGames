using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // A unit cube with rounded edges — the "toy block" every cell, ball block and
    // board plate is made of. Built once per (radius, subdivision) and cached;
    // every renderer shares the instance, so this costs one mesh, not one per cell.
    //
    // Construction: each of the six faces is a (n+1)² grid of points on the unit
    // cube; every point is then pushed onto the rounded surface by clamping it
    // into the inner box (half-size 0.5 − r) and adding r along the direction
    // out of that box. That gives correct smooth normals for free (the same
    // direction), so the bevel shades like a real rounded edge under URP Lit.
    //
    // Runtime-only mesh: scene-built decor keeps Unity primitives (a procedural
    // mesh referenced from a saved scene is not serialised), see GameSceneBuilder.
    public static class RoundedCubeMesh
    {
        private static readonly Dictionary<(int, int), Mesh> _cache = new Dictionary<(int, int), Mesh>();

        // radius 0..0.5 (fraction of the unit edge), subdivisions per face edge (≥ 1).
        public static Mesh Get(float radius = 0.12f, int subdivisions = 4)
        {
            radius = Mathf.Clamp(radius, 0.001f, 0.49f);
            subdivisions = Mathf.Clamp(subdivisions, 1, 12);
            var key = (Mathf.RoundToInt(radius * 1000f), subdivisions);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Build(radius, subdivisions);
            mesh.name = $"RoundedCube_r{radius:0.###}_s{subdivisions}";
            _cache[key] = mesh;
            return mesh;
        }

        private static Mesh Build(float r, int n)
        {
            var verts   = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs     = new List<Vector2>();
            var tris    = new List<int>();

            // Face frames: normal, and the two in-face axes.
            Vector3[] N = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            Vector3[] U = { Vector3.right, Vector3.right, Vector3.right, Vector3.left, Vector3.back, Vector3.forward };
            Vector3[] V = { Vector3.back, Vector3.forward, Vector3.up, Vector3.up, Vector3.up, Vector3.up };

            float h = 0.5f - r;   // inner box half-size

            for (int f = 0; f < 6; f++)
            {
                int baseIndex = verts.Count;
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float a = (i / (float)n) - 0.5f;
                    float b = (j / (float)n) - 0.5f;
                    Vector3 p = N[f] * 0.5f + U[f] * a + V[f] * b;   // on the unit cube face

                    Vector3 inner = new Vector3(Mathf.Clamp(p.x, -h, h), Mathf.Clamp(p.y, -h, h), Mathf.Clamp(p.z, -h, h));
                    Vector3 dir   = p - inner;
                    Vector3 nrm   = dir.sqrMagnitude > 1e-8f ? dir.normalized : N[f];
                    verts.Add(inner + nrm * r);
                    normals.Add(nrm);
                    uvs.Add(new Vector2(i / (float)n, j / (float)n));
                }

                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int i0 = baseIndex + j * (n + 1) + i;
                    int i1 = i0 + 1;
                    int i2 = i0 + (n + 1);
                    int i3 = i2 + 1;
                    // Winding chosen so the face points along its normal.
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
