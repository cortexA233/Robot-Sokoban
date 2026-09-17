using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Sokoban
{
    /// <summary>Board-owned flat marks. Shared meshes/materials are released with the board.</summary>
    internal sealed class StationCircuitMesh : IDisposable
    {
        private readonly List<Object> owned = new List<Object>();
        public readonly Material Ink, Surface, Idle, Power;
        public readonly Mesh Quad, Ring, Plug, Corners, Dot;

        public StationCircuitMesh(StationKitTheme theme)
        {
            Ink = Material(theme, "Ink", new Color(.94f, .96f, .92f));
            Surface = Material(theme, "Surface", new Color(.24f, .31f, .36f));
            Idle = Material(theme, "Idle", new Color(.49f, .62f, .68f));
            Power = Material(theme, "Power", new Color(.35f, .91f, .84f));
            Quad = Own(new Shape().Rect(0, 0, 1, 1).Build("Circuit quad"));
            Ring = Own(new Shape().Ring(.255f, .033f).Build("Goal ring"));
            Dot = Own(new Shape().Ring(.045f, .045f).Build("Circuit terminal"));
            Plug = Own(new Shape().Rect(0, -.012f, .18f, .18f).Rect(-.052f, .117f, .038f, .11f)
                .Rect(.052f, .117f, .038f, .11f).Rect(0, -.137f, .045f, .085f).Build("Gate power plug"));
            var corners = new Shape();
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                corners.Rect(x * .39f, z * .443f, .13f, .026f);
                corners.Rect(x * .443f, z * .39f, .026f, .13f);
            }
            Corners = Own(corners.Build("Goal perimeter corners"));
        }

        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private Material Material(StationKitTheme theme, string role, Color color)
        {
            // Keep the explicitly referenced Unlit/cutout variant used by the shipped label asset.
            // White texture makes the marks opaque; no Shader.Find or unreferenced player variant.
            var material = Own(new Material(theme.labelMaterial) { name = "Station circuit " + role });
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", color); material.SetFloat("_ZWrite", 1);
            return material;
        }

        public MeshRenderer Place(Transform parent, string name, Mesh mesh, Vector3 position, Material material, Vector3? scale = null)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; if (scale.HasValue) obj.transform.localScale = scale.Value;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        public Mesh Wire(Vector3[] points, bool dashed, List<Vector3> crossings)
        {
            var shape = new Shape();
            float phase = 0;
            for (int i = 1; i < points.Length; i++)
            {
                var start = points[i - 1]; var delta = points[i] - start; float length = delta.magnitude;
                if (length < .001f) continue;
                var direction = delta / length;
                for (float d = 0; d < length;)
                {
                    float end = Mathf.Min(length, d + .025f);
                    var middle = start + direction * ((d + end) * .5f);
                    bool gap = crossings.Exists(c => (c - middle).sqrMagnitude < .0121f);
                    if (!gap && (!dashed || (phase + (d + end) * .5f) % .24f < .145f))
                        shape.Bar(start + direction * d, start + direction * end, dashed ? .026f : .040f);
                    d = end;
                }
                phase += length;
            }
            return Own(shape.Build(dashed ? "Inactive circuit path" : "Powered circuit path"));
        }

        public void Dispose() { foreach (var item in owned) if (item) Object.Destroy(item); owned.Clear(); }

        private sealed class Shape
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            public Shape Rect(float x, float z, float width, float depth)
            {
                Face(new Vector3(x-width/2,0,z-depth/2),new Vector3(x+width/2,0,z-depth/2),
                    new Vector3(x+width/2,0,z+depth/2),new Vector3(x-width/2,0,z+depth/2)); return this;
            }
            public Shape Ring(float radius, float width)
            {
                for (int i = 0; i < 64; i++)
                {
                    float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                    var first = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var next = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                    Face(first * radius, next * radius, next * (radius - width), first * (radius - width));
                }
                return this;
            }
            public void Bar(Vector3 start, Vector3 end, float width)
            {
                var normal = Vector3.Cross(Vector3.up, (end - start).normalized) * (width / 2);
                Face(start-normal, end-normal, end+normal, start+normal);
            }
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
