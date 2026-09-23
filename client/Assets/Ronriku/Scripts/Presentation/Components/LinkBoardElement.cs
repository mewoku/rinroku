using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>Square grid for the Logic trial: cells, walls, the drawn path and numbered checkpoints.</summary>
    public sealed class LinkBoardElement : VisualElement
    {
        private readonly LogicPuzzleData _data;
        private IReadOnlyList<int> _path = new List<int>();
        private bool _solved;

        public LinkBoardElement(LogicPuzzleData data)
        {
            _data = data;
            generateVisualContent += Draw;
        }

        public void SetPath(IReadOnlyList<int> path, bool solved)
        {
            _path = path;
            _solved = solved;
            MarkDirtyRepaint();
        }

        /// <summary>Cell index under a local position, or -1.</summary>
        public int CellAt(Vector2 local)
        {
            Layout(out Vector2 origin, out float cell);
            int x = Mathf.FloorToInt((local.x - origin.x) / cell);
            int y = Mathf.FloorToInt((local.y - origin.y) / cell);
            if (x < 0 || y < 0 || x >= _data.Size || y >= _data.Size) return -1;
            return _data.Index(x, y);
        }

        private void Layout(out Vector2 origin, out float cell)
        {
            float side = Mathf.Min(contentRect.width, contentRect.height);
            cell = side / _data.Size;
            origin = new Vector2((contentRect.width - side) * 0.5f, (contentRect.height - side) * 0.5f);
        }

        private Vector2 Center(int index, Vector2 origin, float cell) =>
            origin + new Vector2((index % _data.Size + 0.5f) * cell, (index / _data.Size + 0.5f) * cell);

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            Layout(out Vector2 origin, out float cell);
            float gap = Mathf.Max(3f, cell * 0.06f);
            var visited = new HashSet<int>(_path);

            for (int i = 0; i < _data.CellCount; i++)
            {
                Vector2 c = Center(i, origin, cell);
                float half = (cell - gap) * 0.5f;
                Color color = visited.Contains(i)
                    ? (_solved ? RonrikuTheme.Teal : new Color(0.07f, 0.47f, 0.45f, 1f))
                    : RonrikuTheme.DeepBlue;
                Rect(p, c - new Vector2(half, half), half * 2f, half * 2f, color);
            }

            if (_path.Count > 1)
            {
                p.strokeColor = _solved ? RonrikuTheme.OffWhite : RonrikuTheme.Teal;
                p.lineWidth = cell * 0.22f;
                p.lineJoin = LineJoin.Miter;
                p.lineCap = LineCap.Butt;
                p.BeginPath();
                p.MoveTo(Center(_path[0], origin, cell));
                for (int i = 1; i < _path.Count; i++) p.LineTo(Center(_path[i], origin, cell));
                p.Stroke();
            }

            float wallThickness = Mathf.Max(6f, cell * 0.14f);
            foreach (int key in _data.Walls)
            {
                int a = key / _data.CellCount, b = key % _data.CellCount;
                Vector2 ca = Center(a, origin, cell), cb = Center(b, origin, cell);
                Vector2 mid = (ca + cb) * 0.5f;
                bool vertical = Mathf.Abs(ca.x - cb.x) > 0.5f;
                Vector2 size = vertical ? new Vector2(wallThickness, cell) : new Vector2(cell, wallThickness);
                Rect(p, mid - size * 0.5f, size.x, size.y, RonrikuTheme.Yellow);
            }

            for (int i = 0; i < _data.CellCount; i++)
            {
                int number = _data.Checkpoints[i];
                if (number == 0) continue;
                Vector2 c = Center(i, origin, cell);
                float half = cell * 0.3f;
                bool reached = visited.Contains(i);
                Rect(p, c - new Vector2(half, half), half * 2f, half * 2f, reached ? RonrikuTheme.OffWhite : RonrikuTheme.Yellow);
                string text = number.ToString();
                float pixel = Mathf.Min(half * 1.5f / (text.Length * 6f), half * 1.4f / 7f);
                PixelLabel.DrawCentered(p, text, c, pixel, RonrikuTheme.Black);
            }
        }

        private static void Rect(Painter2D p, Vector2 topLeft, float w, float h, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(topLeft);
            p.LineTo(topLeft + new Vector2(w, 0));
            p.LineTo(topLeft + new Vector2(w, h));
            p.LineTo(topLeft + new Vector2(0, h));
            p.ClosePath();
            p.Fill();
        }
    }
}
