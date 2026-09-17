using System;
using System.Collections.Generic;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    // A fixed plate, deliberately independent from circuit colours and power state.
    public sealed class RedirectorView : MonoBehaviour
    {
        private Mesh arrowMesh;
        public Direction Facing { get; private set; }

        public void Initialize(RedirectorDefinition definition, StationKitTheme theme)
        {
            Facing = (Direction)Enum.Parse(typeof(Direction), definition.facing);
            transform.localRotation = Quaternion.Euler(0, (int)Facing * 90, 0);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Directional plate"; plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0, .009f, 0);
            plate.transform.localScale = new Vector3(.98f, .016f, .98f);
            plate.GetComponent<Renderer>().sharedMaterial = theme.trackSurface;
            var collider = plate.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);

            var vertices = new List<Vector3>(); var triangles = new List<int>();
            AddArrow(vertices, triangles, 0, 0, .72f);
            // These marks sit outside the 0.8 m crate footprint, so a stopped box
            // never hides the authored direction in either camera mode.
            foreach (float side in new[] { -.455f, .455f })
                foreach (float z in new[] { -.19f, .19f }) AddArrow(vertices, triangles, side, z, .1f);
            arrowMesh = new Mesh { name = "Fixed redirector arrows" };
            arrowMesh.SetVertices(vertices); arrowMesh.SetTriangles(triangles, 0); arrowMesh.RecalculateNormals(); arrowMesh.RecalculateBounds();
            var arrows = new GameObject("Direction and edge arrows"); arrows.transform.SetParent(transform, false);
            arrows.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            arrows.AddComponent<MeshRenderer>().sharedMaterial = theme.trackMark;
        }

        private static void AddArrow(List<Vector3> vertices, List<int> triangles, float x, float z, float scale)
        {
            int start = vertices.Count;
            foreach (var point in new[] { new Vector2(-.14f, -.46f), new Vector2(.14f, -.46f), new Vector2(.14f, 0),
                new Vector2(.4f, 0), new Vector2(0, .46f), new Vector2(-.4f, 0), new Vector2(-.14f, 0) })
                vertices.Add(new Vector3(x + point.x * scale, .02f, z + point.y * scale));
            foreach (int index in new[] { 0, 2, 1, 0, 6, 2, 3, 5, 4 }) triangles.Add(start + index);
        }
        private void OnDestroy() { if (arrowMesh) Destroy(arrowMesh); }
    }
}
