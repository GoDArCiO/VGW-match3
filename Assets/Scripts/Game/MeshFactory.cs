using System.Collections.Generic;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes entirely from code — no imported models, no texture pipeline
    /// (the asset doctrine: procedural is the baseline). Every face gets its own vertices so normals are
    /// per-face, giving the crisp faceted look. Meshes are cached by shape key so repeated requests share
    /// one <see cref="Mesh"/>. All meshes are built around their own local origin (centred).
    /// </summary>
    public sealed class MeshFactory
    {
        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        /// <summary>An axis-aligned box of <paramref name="size"/>, centred on the origin.</summary>
        public Mesh Box(Vector3 size)
        {
            string key = $"box:{size.x:0.###},{size.y:0.###},{size.z:0.###}";
            if (_cache.TryGetValue(key, out Mesh cached)) return cached;

            Vector3 h = size * 0.5f;
            var b = new Builder();
            // Eight corners.
            Vector3 ppp = new Vector3(h.x, h.y, h.z), ppm = new Vector3(h.x, h.y, -h.z);
            Vector3 pmp = new Vector3(h.x, -h.y, h.z), pmm = new Vector3(h.x, -h.y, -h.z);
            Vector3 mpp = new Vector3(-h.x, h.y, h.z), mpm = new Vector3(-h.x, h.y, -h.z);
            Vector3 mmp = new Vector3(-h.x, -h.y, h.z), mmm = new Vector3(-h.x, -h.y, -h.z);
            b.Quad(pmp, ppp, ppm, pmm); // +X
            b.Quad(mmm, mpm, mpp, mmp); // -X
            b.Quad(mpp, ppp, ppm, mpm); // +Y (top)
            b.Quad(mmm, pmm, pmp, mmp); // -Y (bottom)
            b.Quad(mmp, pmp, ppp, mpp); // +Z
            b.Quad(pmm, mmm, mpm, ppm); // -Z
            return Store(key, b.Build("Box"));
        }

        /// <summary>A flat quad on the XZ plane facing +Y (a ground/floor tile), centred on the origin.</summary>
        public Mesh Quad(float width, float depth)
        {
            string key = $"quad:{width:0.###},{depth:0.###}";
            if (_cache.TryGetValue(key, out Mesh cached)) return cached;

            float x = width * 0.5f, z = depth * 0.5f;
            var b = new Builder();
            b.Quad(new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(x, 0f, -z));
            return Store(key, b.Build("Quad"));
        }

        /// <summary>A flat-shaded n-gon prism (a low-poly cylinder) of <paramref name="radius"/> and
        /// <paramref name="height"/>, centred on the origin, with <paramref name="sides"/> facets.</summary>
        public Mesh Cylinder(float radius, float height, int sides = 16)
        {
            sides = Mathf.Max(3, sides);
            string key = $"cyl:{radius:0.###},{height:0.###},{sides}";
            if (_cache.TryGetValue(key, out Mesh cached)) return cached;

            float hy = height * 0.5f;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                ring[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }

            var b = new Builder();
            var topC = new Vector3(0f, hy, 0f);
            var botC = new Vector3(0f, -hy, 0f);
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = ring[i], p1 = ring[(i + 1) % sides];
                Vector3 t0 = p0 + Vector3.up * hy, t1 = p1 + Vector3.up * hy;
                Vector3 b0 = p0 - Vector3.up * hy, b1 = p1 - Vector3.up * hy;
                b.Quad(b0, t0, t1, b1);                 // side
                b.Tri(topC, t1, t0);                     // top cap (CCW from above)
                b.Tri(botC, b0, b1);                     // bottom cap
            }
            return Store(key, b.Build("Cylinder"));
        }

        private Mesh Store(string key, Mesh mesh)
        {
            _cache[key] = mesh;
            return mesh;
        }

        /// <summary>Accumulates flat-shaded geometry: each Quad/Tri appends fresh vertices with a per-face
        /// normal, so the result is faceted (never smooth-shaded).</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _normals.Add(n); _normals.Add(n); _normals.Add(n);
                _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
