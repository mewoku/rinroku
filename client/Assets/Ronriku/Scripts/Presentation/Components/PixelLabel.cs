using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    public sealed class PixelLabel : VisualElement
    {
        private static readonly IReadOnlyDictionary<char, string[]> Glyphs = BuildGlyphs();
        private string _text;
        private Color _color;
        private float _pixel;
        private TextAnchor _alignment;

        public PixelLabel(string text, Color color, float pixel = 8f, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            _text = text.ToUpperInvariant();
            _color = color;
            _pixel = pixel;
            _alignment = alignment;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetText(string value)
        {
            _text = (value ?? string.Empty).ToUpperInvariant();
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (string.IsNullOrEmpty(_text)) return;
            float width = _text.Length * 6f * _pixel - _pixel;
            float height = 7f * _pixel;
            float x = _alignment == TextAnchor.MiddleLeft ? 0f : (contentRect.width - width) * 0.5f;
            float y = (contentRect.height - height) * 0.5f;
            var painter = context.painter2D;
            painter.fillColor = _color;
            foreach (char raw in _text)
            {
                char key = Glyphs.ContainsKey(raw) ? raw : '?';
                string[] glyph = Glyphs[key];
                for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                {
                    if (glyph[row][col] != '1') continue;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x + col * _pixel, y + row * _pixel));
                    painter.LineTo(new Vector2(x + (col + 1) * _pixel, y + row * _pixel));
                    painter.LineTo(new Vector2(x + (col + 1) * _pixel, y + (row + 1) * _pixel));
                    painter.LineTo(new Vector2(x + col * _pixel, y + (row + 1) * _pixel));
                    painter.ClosePath();
                    painter.Fill();
                }
                x += 6f * _pixel;
            }
        }

        private static IReadOnlyDictionary<char, string[]> BuildGlyphs()
        {
            string[] rows = {
                "A:01110,10001,10001,11111,10001,10001,10001", "B:11110,10001,10001,11110,10001,10001,11110",
                "C:01111,10000,10000,10000,10000,10000,01111", "D:11110,10001,10001,10001,10001,10001,11110",
                "E:11111,10000,10000,11110,10000,10000,11111", "F:11111,10000,10000,11110,10000,10000,10000",
                "G:01111,10000,10000,10111,10001,10001,01111", "H:10001,10001,10001,11111,10001,10001,10001",
                "I:11111,00100,00100,00100,00100,00100,11111", "J:00111,00010,00010,00010,10010,10010,01100",
                "K:10001,10010,10100,11000,10100,10010,10001", "L:10000,10000,10000,10000,10000,10000,11111",
                "M:10001,11011,10101,10101,10001,10001,10001", "N:10001,11001,10101,10011,10001,10001,10001",
                "O:01110,10001,10001,10001,10001,10001,01110", "P:11110,10001,10001,11110,10000,10000,10000",
                "Q:01110,10001,10001,10001,10101,10010,01101", "R:11110,10001,10001,11110,10100,10010,10001",
                "S:01111,10000,10000,01110,00001,00001,11110", "T:11111,00100,00100,00100,00100,00100,00100",
                "U:10001,10001,10001,10001,10001,10001,01110", "V:10001,10001,10001,10001,10001,01010,00100",
                "W:10001,10001,10001,10101,10101,10101,01010", "X:10001,10001,01010,00100,01010,10001,10001",
                "Y:10001,10001,01010,00100,00100,00100,00100", "Z:11111,00001,00010,00100,01000,10000,11111",
                "0:01110,10011,10101,10101,11001,10001,01110", "1:00100,01100,00100,00100,00100,00100,01110",
                "2:01110,10001,00001,00010,00100,01000,11111", "3:11110,00001,00001,01110,00001,00001,11110",
                "4:00010,00110,01010,10010,11111,00010,00010", "5:11111,10000,10000,11110,00001,00001,11110",
                "6:01110,10000,10000,11110,10001,10001,01110", "7:11111,00001,00010,00100,01000,01000,01000",
                "8:01110,10001,10001,01110,10001,10001,01110", "9:01110,10001,10001,01111,00001,00001,01110",
                " :00000,00000,00000,00000,00000,00000,00000", "?:01110,10001,00010,00100,00100,00000,00100"
            };
            var result = new Dictionary<char, string[]>();
            foreach (string row in rows)
            {
                string[] split = row.Split(':');
                result[split[0][0]] = split[1].Split(',');
            }
            return result;
        }
    }
}

