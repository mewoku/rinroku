using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Compact 3×3 shadow map drawn in the same isometric orientation as the board.
    /// Filled cells are solid; empty cells are outlined, so state never relies on colour alone.
    /// </summary>
    public sealed class ShadowGridElement : VisualElement
    {
        private int _mask;
        private Color _fill;

        public ShadowGridElement(int mask, Color fill)
        {
            _mask = mask;
            _fill = fill;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetMask(int mask, Color fill)
        {
            if (mask == _mask && fill == _fill) return;
            _mask = mask;
            _fill = fill;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float w = Mathf.Min(contentRect.width / 3.2f, contentRect.height / 1.7f);
            float h = w * 0.5f;
            var origin = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.5f);
            p.lineWidth = Mathf.Max(2f, w * 0.05f);
            p.lineJoin = LineJoin.Miter;

            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var c = new Vector2(origin.x + (x - y) * w * 0.5f, origin.y + (x + y - 2) * h * 0.5f);
                float iw = w * 0.86f, ih = h * 0.86f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, c.y - ih * 0.5f));
                p.LineTo(new Vector2(c.x + iw * 0.5f, c.y));
                p.LineTo(new Vector2(c.x, c.y + ih * 0.5f));
                p.LineTo(new Vector2(c.x - iw * 0.5f, c.y));
                p.ClosePath();
                if ((_mask & (1 << (x + 3 * y))) != 0)
                {
                    p.fillColor = _fill;
                    p.Fill();
                }
                else
                {
                    p.strokeColor = RonrikuTheme.BlueGrey;
                    p.Stroke();
                }
            }
        }
    }
}
