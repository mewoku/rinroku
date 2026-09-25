using System;
using System.Collections.Generic;
using Ronriku.Domain.Arcade;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    /// <summary>
    /// Renders one micro-challenge as a card: big one-line prompt, the puzzle, and big tap targets.
    /// Raises <see cref="Answered"/> once with the kind's answer encoding (see Challenge.Answer).
    /// </summary>
    public sealed class ChallengeView : VisualElement
    {
        private readonly Challenge _challenge;
        private readonly Palette _palette;
        private readonly List<VisualElement> _optionButtons = new List<VisualElement>();
        private readonly List<Action<Color?>> _optionFrames = new List<Action<Color?>>();
        private bool _done;
        private int _selection;
        private Label _sumLabel;
        private readonly List<Button> _tiles = new List<Button>();
        private readonly Button[] _cells = new Button[16];

        public event Action<int> Answered;
        /// <summary>Raised when a timed "show" phase (Memory) starts/ends, so the battle can pause its swing clock.</summary>
        public event Action<bool> Showing;

        public Challenge Challenge => _challenge;

        public ChallengeView(Challenge challenge, Palette palette)
        {
            _challenge = challenge;
            _palette = palette;
            name = "challenge";
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.94f);
            UiFactory.SetBorder(this, 2, RonrikuTheme.WithAlpha(palette.Accent, 0.6f));
            style.paddingLeft = style.paddingRight = 12;
            style.paddingTop = 10;
            style.paddingBottom = 12;
            style.alignItems = Align.Stretch;

            var prompt = UiFactory.Heading(challenge.Prompt, 15, palette.Accent);
            prompt.name = "prompt";
            prompt.style.height = 30;
            prompt.style.flexShrink = 0;
            Add(prompt);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.justifyContent = Justify.Center;
            body.style.alignItems = Align.Center;
            Add(body);

            switch (challenge.Kind)
            {
                case ChallengeKind.Next: BuildNext(body); break;
                case ChallengeKind.Odd: BuildOdd(body); break;
                case ChallengeKind.Sum: BuildSum(body); break;
                case ChallengeKind.Memory: BuildMemory(body); break;
                case ChallengeKind.Mirror: BuildMirror(body); break;
                case ChallengeKind.Arrows: BuildArrows(body); break;
                default: BuildScales(body); break;
            }
        }

        private void Submit(int answer)
        {
            if (_done) return;
            _done = true;
            Answered?.Invoke(answer);
        }

        /// <summary>After judging: green frame on the right option, red on a wrong pick.</summary>
        public void Reveal(bool correct, int answer)
        {
            switch (_challenge.Kind)
            {
                case ChallengeKind.Sum:
                    for (int i = 0; i < _tiles.Count; i++)
                    {
                        bool right = (_challenge.Answer & (1 << i)) != 0;
                        if (right) UiFactory.SetBorder(_tiles[i], 3, RonrikuTheme.Teal);
                        else if ((answer & (1 << i)) != 0) UiFactory.SetBorder(_tiles[i], 3, RonrikuTheme.Red);
                    }
                    break;
                case ChallengeKind.Memory:
                    for (int i = 0; i < 16; i++)
                    {
                        bool lit = (_challenge.Answer & (1 << i)) != 0;
                        if (lit) _cells[i].style.backgroundColor = correct ? RonrikuTheme.Teal : RonrikuTheme.WithAlpha(RonrikuTheme.Teal, 0.6f);
                        else if ((answer & (1 << i)) != 0) _cells[i].style.backgroundColor = RonrikuTheme.Red;
                    }
                    break;
                default:
                    if (_challenge.Answer >= 0 && _challenge.Answer < _optionFrames.Count) _optionFrames[_challenge.Answer](RonrikuTheme.Teal);
                    if (!correct && answer >= 0 && answer < _optionFrames.Count) _optionFrames[answer](RonrikuTheme.Red);
                    break;
            }
            if (!correct) Juice.Shake(this, 9f, 0.35f);
        }

        // ------------------------------------------------------------------ option buttons

        private Button OptionButton(int index, VisualElement content, float width, float height)
        {
            var button = new Button(() => Submit(index)) { name = $"option-{index}", text = string.Empty };
            button.style.width = width;
            button.style.height = height;
            button.style.marginLeft = button.style.marginRight = 5;
            button.style.marginTop = button.style.marginBottom = 5;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 6;
            button.style.backgroundColor = RonrikuTheme.Surface2;
            UiFactory.SetBorder(button, 3, RonrikuTheme.WithAlpha(_palette.Accent, 0.45f));
            UiFactory.Pressable(button);
            content.style.flexGrow = 1;
            button.Add(content);
            _optionButtons.Add(button);
            _optionFrames.Add(c => UiFactory.SetBorder(button, 3, c ?? RonrikuTheme.WithAlpha(_palette.Accent, 0.45f)));
            return button;
        }

        private static VisualElement WrapRow()
        {
            var row = UiFactory.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            return row;
        }

        // ------------------------------------------------------------------ Next

        private void BuildNext(VisualElement body)
        {
            var row = UiFactory.Row();
            row.style.justifyContent = Justify.Center;
            row.style.marginBottom = 18;
            var tokens = new List<TokenElement>();
            for (int i = 0; i < _challenge.Items.Length; i++)
            {
                var t = new TokenElement(_challenge.Items[i]);
                t.style.width = t.style.height = 54;
                t.style.marginLeft = t.style.marginRight = 2;
                row.Add(t);
                tokens.Add(t);
            }
            var slot = new TokenElement(0, true);
            slot.style.width = slot.style.height = 54;
            slot.style.marginLeft = 2;
            slot.SetFrame(RonrikuTheme.Yellow);
            row.Add(slot);
            body.Add(row);

            // The row "reads itself": tokens pop left→right, ending on the question mark.
            int beat = 0;
            schedule.Execute(() =>
            {
                if (_done || MotionSettings.ReducedMotion) return;
                int k = beat++ % (tokens.Count + 3);
                if (k < tokens.Count) Juice.Punch(tokens[k], 0.14f, 0.2f);
                else if (k == tokens.Count) Juice.Punch(slot, 0.2f, 0.25f);
            }).Every(260);

            var options = WrapRow();
            for (int i = 0; i < _challenge.Options.Length; i++)
            {
                var token = new TokenElement(_challenge.Options[i]);
                options.Add(OptionButton(i, token, 100, 100));
            }
            body.Add(options);
        }

        // ------------------------------------------------------------------ Odd

        private void BuildOdd(VisualElement body)
        {
            var grid = WrapRow();
            grid.style.width = 290;
            Color fill = _palette.Accent;
            for (int i = 0; i < _challenge.Options.Length; i++)
            {
                var pixels = new PixelGridElement(_challenge.Options[i], fill);
                int index = i;
                var button = OptionButton(i, pixels, 128, 128);
                _optionFrames[index] = c =>
                {
                    UiFactory.SetBorder(button, 3, c ?? RonrikuTheme.WithAlpha(_palette.Accent, 0.45f));
                    pixels.SetFrame(c);
                };
                grid.Add(button);
            }
            body.Add(grid);
        }

        // ------------------------------------------------------------------ Sum

        private void BuildSum(VisualElement body)
        {
            var target = new PixelLabel(_challenge.Target.ToString(), RonrikuTheme.Yellow, 6);
            target.style.height = 58;
            target.style.width = 200;
            body.Add(target);
            _sumLabel = UiFactory.Heading("PICKED 0", 12, RonrikuTheme.Muted);
            _sumLabel.style.marginBottom = 12;
            body.Add(_sumLabel);
            var tiles = WrapRow();
            tiles.style.width = 330;
            for (int i = 0; i < _challenge.Items.Length; i++)
            {
                int index = i;
                var tile = new Button(() => ToggleTile(index)) { name = $"tile-{i}", text = _challenge.Items[i].ToString() };
                tile.style.width = 92;
                tile.style.height = 84;
                tile.style.marginLeft = tile.style.marginRight = tile.style.marginTop = tile.style.marginBottom = 6;
                tile.style.fontSize = 34;
                tile.style.unityFontDefinition = RonrikuTheme.DisplayBold;
                tile.style.color = RonrikuTheme.Text;
                tile.style.backgroundColor = RonrikuTheme.Surface2;
                UiFactory.SetBorder(tile, 3, RonrikuTheme.Line);
                UiFactory.Pressable(tile);
                _tiles.Add(tile);
                tiles.Add(tile);
            }
            body.Add(tiles);
        }

        private void ToggleTile(int index)
        {
            if (_done) return;
            _selection ^= 1 << index;
            bool on = (_selection & (1 << index)) != 0;
            var tile = _tiles[index];
            tile.style.backgroundColor = on ? RonrikuTheme.WithAlpha(_palette.Accent, 0.35f) : RonrikuTheme.Surface2;
            UiFactory.SetBorder(tile, 3, on ? _palette.Accent : RonrikuTheme.Line);
            Juice.Punch(tile, 0.12f);
            int sum = Challenges.SumOf(_challenge.Items, _selection);
            int picked = Challenges.PopCount(_selection);
            _sumLabel.text = $"PICKED {sum}";
            _sumLabel.style.color = sum == _challenge.Target ? RonrikuTheme.Teal : sum > _challenge.Target ? RonrikuTheme.Red : RonrikuTheme.Muted;
            if (picked == _challenge.Pick || sum > _challenge.Target + 9) Submit(_selection);
        }

        // ------------------------------------------------------------------ Memory

        private void BuildMemory(VisualElement body)
        {
            var grid = WrapRow();
            grid.style.width = 300;
            for (int i = 0; i < 16; i++)
            {
                int index = i;
                var cell = new Button(() => TapCell(index)) { name = $"cell-{i}", text = string.Empty };
                cell.style.width = cell.style.height = 62;
                cell.style.marginLeft = cell.style.marginRight = cell.style.marginTop = cell.style.marginBottom = 5;
                cell.style.backgroundColor = RonrikuTheme.Surface2;
                UiFactory.SetBorder(cell, 2, RonrikuTheme.Line);
                cell.SetEnabled(false);
                _cells[i] = cell;
                grid.Add(cell);
            }
            body.Add(grid);

            // Show phase: lit cells glow, then everything goes dark and the grid unlocks.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Showing?.Invoke(true);
                for (int i = 0; i < 16; i++)
                    if ((_challenge.Answer & (1 << i)) != 0)
                    {
                        _cells[i].style.backgroundColor = RonrikuTheme.Yellow;
                        UiFactory.SetBorder(_cells[i], 2, Color.white);
                    }
                Feedback.Snap();
                schedule.Execute(() =>
                {
                    for (int i = 0; i < 16; i++)
                    {
                        _cells[i].style.backgroundColor = RonrikuTheme.Surface2;
                        UiFactory.SetBorder(_cells[i], 2, RonrikuTheme.Line);
                        _cells[i].SetEnabled(true);
                        UiFactory.Pressable(_cells[i]);
                    }
                    Feedback.Whoosh();
                    Showing?.Invoke(false);
                }).StartingIn(1300 + 150 * _challenge.Pick);
            });
        }

        private void TapCell(int index)
        {
            if (_done || (_selection & (1 << index)) != 0) return;
            _selection |= 1 << index;
            bool right = (_challenge.Answer & (1 << index)) != 0;
            _cells[index].style.backgroundColor = right ? _palette.Accent : RonrikuTheme.Red;
            Juice.Punch(_cells[index], 0.15f);
            if (!right || _selection == _challenge.Answer) Submit(_selection);
            else Feedback.Snap();
        }

        // ------------------------------------------------------------------ Mirror

        private void BuildMirror(VisualElement body)
        {
            var picture = new HalfGrid(_challenge.Items[0], -1, _palette.Accent);
            picture.style.width = 170;
            picture.style.height = 170;
            picture.style.marginBottom = 16;
            body.Add(picture);
            var options = WrapRow();
            for (int i = 0; i < _challenge.Options.Length; i++)
            {
                var half = new HalfGrid(-1, _challenge.Options[i], _palette.Accent);
                options.Add(OptionButton(i, half, 86, 140));
            }
            body.Add(options);
        }

        // ------------------------------------------------------------------ Arrows

        private void BuildArrows(VisualElement body)
        {
            var board = new ArrowBoard(_challenge, _palette.Accent);
            board.style.width = board.style.height = 250;
            board.style.marginBottom = 12;
            body.Add(board);
            var options = WrapRow();
            for (int i = 0; i < _challenge.Options.Length; i++)
            {
                var letter = UiFactory.Heading(((char)('A' + i)).ToString(), 26, RonrikuTheme.Yellow);
                options.Add(OptionButton(i, letter, 96, 64));
            }
            body.Add(options);
        }

        // ------------------------------------------------------------------ Scales

        private void BuildScales(VisualElement body)
        {
            foreach (int[] clue in _challenge.Groups)
            {
                var eq = new EquationRow(new[] { clue[0] }, Slice(clue, 1), null, _palette.Accent);
                eq.style.height = 58;
                eq.style.width = 340;
                eq.style.marginBottom = 8;
                body.Add(eq);
            }
            var question = new EquationRow(_challenge.Items, Array.Empty<int>(), "?", _palette.Accent);
            question.style.height = 64;
            question.style.width = 340;
            question.style.marginTop = 8;
            question.style.marginBottom = 14;
            body.Add(question);
            var options = WrapRow();
            for (int i = 0; i < _challenge.Options.Length; i++)
            {
                var label = UiFactory.Heading(_challenge.Options[i].ToString(), 30, RonrikuTheme.Text);
                options.Add(OptionButton(i, label, 96, 72));
            }
            body.Add(options);
        }

        private static int[] Slice(int[] source, int from)
        {
            var result = new int[source.Length - from];
            Array.Copy(source, from, result, 0, result.Length);
            return result;
        }
    }

    /// <summary>4×4 mirror picture: left half from a mask and/or right half from a mask, mirror line in the middle.</summary>
    internal sealed class HalfGrid : VisualElement
    {
        private readonly int _left;
        private readonly int _right;
        private readonly Color _fill;

        public HalfGrid(int left, int right, Color fill)
        {
            _left = left;
            _right = right;
            _fill = fill;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            bool full = _left >= 0;
            int cols = full ? 4 : 2;
            float cell = Mathf.Min(contentRect.width / cols, contentRect.height / 4f);
            var origin = new Vector2((contentRect.width - cell * cols) * 0.5f, (contentRect.height - cell * 4) * 0.5f);
            Glyphs.Rect(p, origin, cell * cols, cell * 4, RonrikuTheme.NearBlack);
            for (int y = 0; y < 4; y++)
            for (int c = 0; c < cols; c++)
            {
                var tl = origin + new Vector2(c * cell + 2, y * cell + 2);
                float s = cell - 4;
                bool filled;
                bool unknown = false;
                if (full && c < 2) filled = (_left & (1 << (c + 2 * y))) != 0;
                else if (full) { filled = false; unknown = true; }
                else filled = (_right & (1 << (c + 2 * y))) != 0;
                if (filled) Glyphs.Rect(p, tl, s, s, _fill);
                else Glyphs.Rect(p, tl + new Vector2(s * 0.4f, s * 0.4f), s * 0.2f, s * 0.2f, unknown ? RonrikuTheme.Yellow : RonrikuTheme.BlueGrey);
            }
            if (full) Glyphs.Rect(p, origin + new Vector2(cell * 2 - 1.5f, -6), 3, cell * 4 + 12, RonrikuTheme.Yellow);
        }
    }

    /// <summary>Arrow maze with a ball on the start cell and the three answer cells lettered.</summary>
    internal sealed class ArrowBoard : VisualElement
    {
        private readonly Challenge _c;
        private readonly Color _accent;

        public ArrowBoard(Challenge c, Color accent)
        {
            _c = c;
            _accent = accent;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            int n = Challenges.ArrowSize;
            float cell = Mathf.Min(contentRect.width, contentRect.height) / n;
            var origin = new Vector2((contentRect.width - cell * n) * 0.5f, (contentRect.height - cell * n) * 0.5f);
            for (int i = 0; i < n * n; i++)
            {
                int x = i % n, y = i / n;
                var tl = origin + new Vector2(x * cell + 2, y * cell + 2);
                int option = Array.IndexOf(_c.Options, i);
                Glyphs.Rect(p, tl, cell - 4, cell - 4, option >= 0 ? RonrikuTheme.WithAlpha(RonrikuTheme.Yellow, 0.18f) : RonrikuTheme.NearBlack);
                var center = tl + new Vector2(cell - 4, cell - 4) * 0.5f;
                Glyphs.Shape(p, Token.Arrow, center, cell * 0.28f, i == _c.Target ? RonrikuTheme.Text : RonrikuTheme.BlueGrey, _c.Items[i]);
                if (i == _c.Target) Glyphs.Shape(p, Token.Circle, center, cell * 0.2f, _accent);
                if (option >= 0) PixelLabel.DrawCentered(p, ((char)('A' + option)).ToString(), tl + new Vector2(cell * 0.2f, cell * 0.2f), cell * 0.035f, RonrikuTheme.Yellow);
            }
        }
    }

    /// <summary>"shapes = shapes" or "shapes = ?" drawn as pixel glyphs.</summary>
    internal sealed class EquationRow : VisualElement
    {
        private static readonly int[] ShapeMap = { Token.Circle, Token.Square, Token.Triangle };
        private static readonly Color[] ShapeColors = { RonrikuTheme.Hex("E8DA37"), RonrikuTheme.Hex("3A9BFF"), RonrikuTheme.Hex("FF4FD8") };
        private readonly int[] _left;
        private readonly int[] _right;
        private readonly string _rightText;

        public EquationRow(int[] left, int[] right, string rightText, Color accent)
        {
            _left = left;
            _right = right;
            _rightText = rightText;
            pickingMode = PickingMode.Ignore;
            style.backgroundColor = RonrikuTheme.NearBlack;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float h = contentRect.height;
            float r = Mathf.Min(h * 0.3f, 16f);
            float step = r * 2.3f;
            float mid = contentRect.width * 0.5f;
            float cy = h * 0.5f;
            // Left side, right-aligned to the '=' sign.
            for (int i = 0; i < _left.Length; i++)
            {
                float x = mid - 26 - (_left.Length - 1 - i) * step - r;
                DrawShape(p, _left[i], new Vector2(x, cy), r);
            }
            Glyphs.Rect(p, new Vector2(mid - 10, cy - 6), 20, 4, RonrikuTheme.Text);
            Glyphs.Rect(p, new Vector2(mid - 10, cy + 2), 20, 4, RonrikuTheme.Text);
            if (_rightText != null)
            {
                PixelLabel.DrawCentered(p, _rightText, new Vector2(mid + 26 + r, cy), r * 0.22f, RonrikuTheme.Yellow);
                return;
            }
            for (int i = 0; i < _right.Length; i++)
                DrawShape(p, _right[i], new Vector2(mid + 26 + r + i * step * (_right.Length > 4 ? 0.8f : 1f), cy), _right[i] == 0 ? r * 0.6f : r);
        }

        private static void DrawShape(Painter2D p, int shape, Vector2 c, float r) =>
            Glyphs.Shape(p, ShapeMap[shape], c, shape == 0 ? r * 0.6f : r, ShapeColors[shape]);
    }
}
