using System.Collections.Generic;
using Ronriku.Domain.Figures;
using UnityEngine;

namespace Ronriku.Presentation.Voxels
{
    /// <summary>
    /// Builds a mesh with one quad per exposed voxel face. Domain axes (x right, y front→back, z up) map to
    /// Unity (x, z, y). The mesh is centred on the ground plane (y = 0 at the feet).
    /// </summary>
    public static class VoxelMeshBuilder
    {
        private static readonly Vector3Int[] Directions =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        public static Mesh Build(Figure figure)
        {
            var colours = new Color[Figure.PaletteSize + 1];
            for (int i = 0; i < Figure.PaletteSize; i++)
            {
                uint rgb = figure.Palette[i];
                colours[i + 1] = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f).linear;
            }
            return Build(figure.Size, figure.Size, figure.Height, (x, y, z) => figure.Get(x, y, z), colours);
        }

        /// <summary>Generic builder for any grid. <paramref name="get"/> returns 0 for empty.</summary>
        public static Mesh Build(int sx, int sy, int sz, System.Func<int, int, int, int> get, Color[] colours)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var offset = new Vector3(-sx * 0.5f, 0f, -sy * 0.5f);

            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                int v = get(x, y, z);
                if (v == 0) continue;
                foreach (Vector3Int d in Directions)
                {
                    int nx = x + d.x, ny = y + d.y, nz = z + d.z;
                    bool inside = nx >= 0 && ny >= 0 && nz >= 0 && nx < sx && ny < sy && nz < sz;
                    if (inside && get(nx, ny, nz) != 0) continue;
                    AddFace(vertices, normals, colors, triangles, new Vector3(x, z, y) + offset,
                        new Vector3(d.x, d.z, d.y), colours[Mathf.Clamp(v, 0, colours.Length - 1)]);
                }
            }

            var mesh = new Mesh { name = "voxels" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles,
            Vector3 origin, Vector3 normal, Color colour)
        {
            // Two tangent axes perpendicular to the normal, chosen so the winding faces outwards.
            Vector3 a, b;
            if (normal.x != 0) { a = new Vector3(0, 0, 1); b = new Vector3(0, 1, 0); }
            else if (normal.y != 0) { a = new Vector3(1, 0, 0); b = new Vector3(0, 0, 1); }
            else { a = new Vector3(0, 1, 0); b = new Vector3(1, 0, 0); }
            if (normal.x + normal.y + normal.z < 0) (a, b) = (b, a);

            Vector3 centre = origin + Vector3.one * 0.5f + normal * 0.5f;
            Vector3 ha = a * 0.5f, hb = b * 0.5f;
            int start = vertices.Count;
            vertices.Add(centre - ha - hb);
            vertices.Add(centre - ha + hb);
            vertices.Add(centre + ha + hb);
            vertices.Add(centre + ha - hb);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
                colors.Add(colour);
            }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
    }
}
