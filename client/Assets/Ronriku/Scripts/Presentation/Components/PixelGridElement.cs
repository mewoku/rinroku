using Ronriku.Domain.Puzzles;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Square 4×4 pattern grid. Filled cells are solid; empty cells are small dots, so the
    /// pattern reads by shape as well as colour. An optional frame marks selection or feedback.
    /// </summary>
    public sealed class PixelGridElement : VisualElement
    {
        private int _mask;
        private Color _fill;
        private Color? _frame;
        private bool _unknown;

        public PixelGridElement(int mask, Color fill, bool unknown = false)
        {
            _mask = mask;
            _fill = fill;
            _unknown = unknown;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetFrame(Color? frame)
        {
            _frame = frame;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float side = Mathf.Min(contentRect.width, contentRect.height);
            var origin = new Vector2((contentRect.width - side) * 0.5f, (contentRect.height - side) * 0.5f);
            float pad = side * 0.08f;
            float cell = (side - pad * 2f) / PatternGrid.Size;
            float gap = cell * 0.1f;

            Rect(p, origin, side, side, RonrikuTheme.NearBlack);
            if (_frame.HasValue)
            {
                float w = Mathf.Max(3f, side * 0.035f);
                Rect(p, origin, side, w, _frame.Value);
                Rect(p, origin + new Vector2(0, side - w), side, w, _frame.Value);
                Rect(p, origin, w, side, _frame.Value);
                Rect(p, origin + new Vector2(side - w, 0), w, side, _frame.Value);
            }

            if (_unknown)
            {
                PixelLabel.DrawCentered(p, "?", origin + new Vector2(side, side) * 0.5f, side * 0.07f, RonrikuTheme.Yellow);
                return;
            }

            for (int y = 0; y < PatternGrid.Size; y++)
            for (int x = 0; x < PatternGrid.Size; x++)
            {
                var topLeft = origin + new Vector2(pad + x * cell + gap * 0.5f, pad + y * cell + gap * 0.5f);
                float size = cell - gap;
                if (PatternGrid.Get(_mask, x, y)) Rect(p, topLeft, size, size, _fill);
                else
                {
                    float dot = size * 0.18f;
                    Rect(p, topLeft + new Vector2((size - dot) * 0.5f, (size - dot) * 0.5f), dot, dot, RonrikuTheme.BlueGrey);
                }
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
