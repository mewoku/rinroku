using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    public sealed class IsometricBoardElement : VisualElement
    {
        private IReadOnlyList<GridPoint> _cubes;
        private float _quarterTurns;
        private readonly bool _compact;

        public IsometricBoardElement(IReadOnlyList<GridPoint> cubes, int orientation = 0, bool compact = false)
        {
            _cubes = cubes;
            _quarterTurns = orientation & 3;
            _compact = compact;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetOrientation(int value)
        {
            _quarterTurns = value & 3;
            MarkDirtyRepaint();
        }

        public void SetDisplayRotation(float quarterTurns)
        {
            _quarterTurns = quarterTurns;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float tileW = Mathf.Min(contentRect.width / 5.4f, contentRect.height / 4.5f);
            float tileH = tileW * 0.48f;
            float centerX = contentRect.width * 0.5f;
            float centerY = contentRect.height * (_compact ? 0.53f : 0.60f);

            for (int sum = 0; sum <= 4; sum++)
            for (int x = 0; x < 3; x++)
            {
                int y = sum - x;
                if (y < 0 || y > 2) continue;
                Vector2 center = Iso(x - 1, y - 1, 0, centerX, centerY, tileW, tileH);
                Diamond(painter, center, tileW, tileH, ((x + y) & 1) == 0 ? RonrikuTheme.Teal : RonrikuTheme.DeepBlue);
            }

            var rotated = new List<ProjectedCube>(_cubes.Count);
            foreach (var cube in _cubes) rotated.Add(Rotate(cube, _quarterTurns));
            rotated.Sort((a, b) => (a.X + a.Y + a.Z * 2).CompareTo(b.X + b.Y + b.Z * 2));
            foreach (var cube in rotated)
            {
                Vector2 top = Iso(cube.X - 1, cube.Y - 1, cube.Z + 1, centerX, centerY, tileW, tileH);
                Cube(painter, top, tileW * 0.82f, tileH * 0.82f);
            }
        }

        private static ProjectedCube Rotate(GridPoint point, float quarterTurns)
        {
            float radians = quarterTurns * Mathf.PI * 0.5f;
            float x = point.X - 1;
            float y = point.Y - 1;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new ProjectedCube(x * cos - y * sin + 1, x * sin + y * cos + 1, point.Z);
        }

        private readonly struct ProjectedCube
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Z;

            public ProjectedCube(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        private static Vector2 Iso(float x, float y, float z, float centerX, float centerY, float w, float h) =>
            new Vector2(centerX + (x - y) * w * 0.5f, centerY + (x + y) * h * 0.5f - z * h * 1.52f);

        private static void Diamond(Painter2D p, Vector2 c, float w, float h, Color color)
        {
            Polygon(p, color, new Vector2(c.x, c.y - h * 0.5f), new Vector2(c.x + w * 0.5f, c.y),
                new Vector2(c.x, c.y + h * 0.5f), new Vector2(c.x - w * 0.5f, c.y));
        }

        private static void Cube(Painter2D p, Vector2 top, float w, float h)
        {
            float depth = h * 1.12f;
            var a = new Vector2(top.x, top.y - h * 0.5f);
            var b = new Vector2(top.x + w * 0.5f, top.y);
            var c = new Vector2(top.x, top.y + h * 0.5f);
            var d = new Vector2(top.x - w * 0.5f, top.y);
            var c2 = c + Vector2.up * depth;
            var b2 = b + Vector2.up * depth;
            var d2 = d + Vector2.up * depth;
            Polygon(p, RonrikuTheme.Yellow * new Color(0.76f, 0.76f, 0.76f, 1f), d, c, c2, d2);
            Polygon(p, RonrikuTheme.Yellow * new Color(0.88f, 0.88f, 0.88f, 1f), c, b, b2, c2);
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
