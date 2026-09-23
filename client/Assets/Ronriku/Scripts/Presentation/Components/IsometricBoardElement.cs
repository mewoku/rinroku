using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Flat-shaded isometric view of a 3×3 floor and a rigid cube structure.
    /// The structure rotates about the box centre and is dropped to rest on the floor.
    /// When a target shadow is supplied, target tiles are teal and the current
    /// top-down shadow is drawn over the floor.
    /// </summary>
    public sealed class IsometricBoardElement : VisualElement
    {
        private const float TileAspect = 0.5f;
        private const float CubeHeight = 0.58f;
        private const float SlabDepth = 0.34f;
        private const float TileInset = 0.94f;

        private static readonly Color ShadowColor = new Color(0.035f, 0.04f, 0.047f, 0.62f);
        private static readonly Color SlabLeft = new Color(0.047f, 0.23f, 0.29f, 1f);
        private static readonly Color SlabRight = new Color(0.035f, 0.17f, 0.22f, 1f);

        private readonly IReadOnlyList<GridPoint> _cubes;
        private readonly int _targetShadow;
        private readonly List<Vector3> _rotated = new List<Vector3>();
        private readonly HashSet<Vector2Int> _shadowCells = new HashSet<Vector2Int>();
        private Quaternion _rotation;

        public IsometricBoardElement(IReadOnlyList<GridPoint> cubes, int orientation = 0, int targetShadow = -1)
        {
            _cubes = cubes;
            _targetShadow = targetShadow;
            _rotation = ToQuaternion(orientation);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetOrientation(int orientation) => SetDisplayRotation(ToQuaternion(orientation));

        public void SetDisplayRotation(Quaternion rotation)
        {
            _rotation = rotation;
            MarkDirtyRepaint();
        }

        public static Quaternion ToQuaternion(int orientation)
        {
            IReadOnlyList<int> m = CubeOrientations.Matrix(
                ((orientation % CubeOrientations.Count) + CubeOrientations.Count) % CubeOrientations.Count);
            var matrix = Matrix4x4.identity;
            matrix.m00 = m[0]; matrix.m01 = m[1]; matrix.m02 = m[2];
            matrix.m10 = m[3]; matrix.m11 = m[4]; matrix.m12 = m[5];
            matrix.m20 = m[6]; matrix.m21 = m[7]; matrix.m22 = m[8];
            return matrix.rotation;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float w = Mathf.Min(contentRect.width / 3.4f, contentRect.height / 4.3f);
            float h = w * TileAspect;
            float v = w * CubeHeight;
            var origin = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.5f + v * 1.2f);

            DrawSlab(painter, origin, w, h, v);
            for (int sum = 0; sum <= 4; sum++)
            for (int x = 0; x < 3; x++)
            {
                int y = sum - x;
                if (y < 0 || y > 2) continue;
                Color color = _targetShadow < 0
                    ? (((x + y) & 1) == 0 ? RonrikuTheme.Teal : RonrikuTheme.DeepBlue)
                    : ((_targetShadow & (1 << (x + 3 * y))) != 0 ? RonrikuTheme.Teal : RonrikuTheme.DeepBlue);
                Diamond(painter, Iso(origin, x - 1, y - 1, 0, w, h, v), w * TileInset, h * TileInset, color);
            }

            _rotated.Clear();
            float minZ = float.MaxValue;
            foreach (var cube in _cubes)
            {
                Vector3 r = _rotation * new Vector3(cube.X - 1, cube.Y - 1, cube.Z - 1);
                _rotated.Add(r);
                minZ = Mathf.Min(minZ, r.z);
            }

            if (_targetShadow >= 0)
            {
                _shadowCells.Clear();
                foreach (var r in _rotated)
                {
                    if (!_shadowCells.Add(new Vector2Int(Mathf.RoundToInt(r.x * 50f), Mathf.RoundToInt(r.y * 50f))))
                        continue;
                    Diamond(painter, Iso(origin, r.x, r.y, 0, w, h, v), w * TileInset, h * TileInset, ShadowColor);
                }
            }

            _rotated.Sort((a, b) =>
            {
                int depth = (a.x + a.y + a.z).CompareTo(b.x + b.y + b.z);
                return depth != 0 ? depth : a.z.CompareTo(b.z);
            });
            foreach (var r in _rotated)
                Cube(painter, Iso(origin, r.x, r.y, r.z - minZ + 1f, w, h, v), w, h, v);
        }

        private static Vector2 Iso(Vector2 origin, float x, float y, float z, float w, float h, float v) =>
            new Vector2(origin.x + (x - y) * w * 0.5f, origin.y + (x + y) * h * 0.5f - z * v);

        private static void DrawSlab(Painter2D p, Vector2 origin, float w, float h, float v)
        {
            var left = new Vector2(origin.x - w * 1.5f, origin.y);
            var bottom = new Vector2(origin.x, origin.y + h * 1.5f);
            var right = new Vector2(origin.x + w * 1.5f, origin.y);
            var down = Vector2.up * (v * SlabDepth);
            Polygon(p, SlabLeft, left, bottom, bottom + down, left + down);
            Polygon(p, SlabRight, bottom, right, right + down, bottom + down);
        }

        private static void Diamond(Painter2D p, Vector2 c, float w, float h, Color color)
        {
            Polygon(p, color, new Vector2(c.x, c.y - h * 0.5f), new Vector2(c.x + w * 0.5f, c.y),
                new Vector2(c.x, c.y + h * 0.5f), new Vector2(c.x - w * 0.5f, c.y));
        }

        private static void Cube(Painter2D p, Vector2 top, float w, float h, float v)
        {
            float s = 0.96f;
            w *= s; h *= s; v *= s;
            var a = new Vector2(top.x, top.y - h * 0.5f);
            var b = new Vector2(top.x + w * 0.5f, top.y);
            var c = new Vector2(top.x, top.y + h * 0.5f);
            var d = new Vector2(top.x - w * 0.5f, top.y);
            var down = Vector2.up * v;
            Polygon(p, RonrikuTheme.Yellow * new Color(0.72f, 0.72f, 0.72f, 1f), d, c, c + down, d + down);
            Polygon(p, RonrikuTheme.Yellow * new Color(0.86f, 0.86f, 0.86f, 1f), c, b, b + down, c + down);
            Polygon(p, RonrikuTheme.Yellow, a, b, c, d);
        }

        private static void Polygon(Painter2D p, Color color, params Vector2[] points)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
        }
    }
}
