using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Pattern trial: example outputs repeatedly "scan in" from their inputs so the rule is shown, not
    /// told; the missing output pulses; one tap answers.
    /// </summary>
    public sealed class PatternPuzzleScreen : TrialScreenBase
    {
        private readonly PatternPuzzleData _data;
        private readonly PatternPuzzleValidator _validator = new PatternPuzzleValidator();
        private readonly PixelGridElement[] _optionGrids;
        private readonly VisualElement[] _optionButtons;
        private int _chosen = -1;
        private readonly PatternRule? _rule;
        private PixelGridElement _testGrid;

        public PatternPuzzleScreen(PatternPuzzleData data, TrialScreenContext context)
            : base(context, "PATTERN", "WATCH THE CHANGE. DO IT TO THE YELLOW ONE", data.Metadata.TimeLimitSeconds,
                data.Metadata.ContentHash)
        {
            _data = data;
            _rule = InferRule(data);
            Body.style.justifyContent = Justify.Center;

            float grid = data.Examples.Count > 2 ? 80 : 96;
            for (int i = 0; i < data.Examples.Count; i++)
            {
                var (input, output) = data.Examples[i];
                Body.Add(ExampleRow(input, output, false, grid, i));
            }
            Body.Add(ExampleRow(data.TestInput, 0, true, grid, data.Examples.Count));

            var options = UiFactory.Row();
            options.style.justifyContent = Justify.SpaceBetween;
            options.style.marginTop = 14;
            _optionGrids = new PixelGridElement[data.Options.Count];
            _optionButtons = new VisualElement[data.Options.Count];
            for (int i = 0; i < data.Options.Count; i++)
            {
                int index = i;
                var button = new Button(() => Choose(index)) { name = $"option-{i}", text = string.Empty };
                button.style.width = Length.Percent(23.5f);
                button.style.height = 104;
                button.style.marginLeft = button.style.marginRight = 0;
                button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 4;
                button.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Surface2, 0.9f);
                UiFactory.SetBorder(button, 2, RonrikuTheme.WithAlpha(Palette.Accent, 0.45f));
                UiFactory.Pressable(button);
                var pixels = new PixelGridElement(data.Options[i], RonrikuTheme.Text);
                pixels.style.flexGrow = 1;
                button.Add(pixels);
                var letter = UiFactory.Heading(((char)('A' + i)).ToString(), 10, RonrikuTheme.Muted);
                letter.style.height = 16;
                button.Add(letter);
                _optionGrids[i] = pixels;
                _optionButtons[i] = button;
                options.Add(button);
            }
            Body.Add(options);

            SetStatus("WHICH ONE DOES YELLOW BECOME?", RonrikuTheme.Muted);
            UpdateCounter();
        }

        protected override string CounterPrefix() => $"{_data.Examples.Count} CLUES";

        protected override TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds)
        {
            int target = TrialScoring.TargetMilliseconds("pattern", _data.Metadata.Difficulty);
            int moves = _chosen >= 0 ? 1 : 0;
            int points = TrialScoring.Points(_data.Metadata.Difficulty, solved, elapsedMilliseconds, target, moves, 1, 0, 0);
            return new TrialOutcome(TrialKind.Pattern, _data.Metadata.Difficulty, solved, elapsedMilliseconds, target,
                moves, 0, 0, points, _chosen >= 0 ? _chosen.ToString() : null);
        }

        private void Choose(int index)
        {
            if (_chosen >= 0) return;
            _chosen = index;
            bool correct = _validator.IsCorrect(_data, index);
            _optionGrids[_data.CorrectOption].SetFrame(RonrikuTheme.Teal);
            UiFactory.SetBorder(_optionButtons[_data.CorrectOption], 2, RonrikuTheme.Teal);
            if (!correct)
            {
                _optionGrids[index].SetFrame(RonrikuTheme.Red);
                UiFactory.SetBorder(_optionButtons[index], 2, RonrikuTheme.Red);
            }
            MarkFinished(correct, correct ? "EXACTLY" : "NOT THAT ONE  ·  ANSWER IN TEAL", correct ? RonrikuTheme.Teal : RonrikuTheme.Red);
            if (_testGrid != null && _rule.HasValue)
                Morph(_testGrid, _data.TestInput, _data.Options[_data.CorrectOption], _rule.Value, () => { });
        }

        /// <summary>The single primitive that explains every example (Daily/Standard uses one rule).</summary>
        private static PatternRule? InferRule(PatternPuzzleData data)
        {
            foreach (PatternRule rule in (PatternRule[])System.Enum.GetValues(typeof(PatternRule)))
            {
                bool fits = true;
                foreach (var (input, output) in data.Examples)
                    if (PatternGrid.Apply(rule, input) != output) { fits = false; break; }
                if (fits) return rule;
            }
            return null;
        }

        private VisualElement ExampleRow(int input, int output, bool isTest, float size, int index)
        {
            var row = UiFactory.Row();
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 8;

            var left = new PixelGridElement(input, isTest ? RonrikuTheme.Yellow : Palette.Accent);
            left.style.width = left.style.height = size;
            row.Add(left);
            if (isTest) _testGrid = left;

            var arrow = new PixelIcon("play", RonrikuTheme.Muted, 20);
            arrow.style.marginLeft = arrow.style.marginRight = 18;
            row.Add(arrow);

            var right = new PixelGridElement(isTest ? 0 : output, Palette.Accent2 == default ? Palette.Accent : Color.Lerp(Palette.Accent, Palette.Accent2, 0.35f), isTest);
            right.style.width = right.style.height = size;
            if (isTest) right.SetFrame(RonrikuTheme.Yellow);
            row.Add(right);

            if (isTest)
            {
                row.schedule.Execute(() =>
                {
                    if (_chosen >= 0 || MotionSettings.ReducedMotion) return;
                    right.style.scale = new Scale(Vector3.one * (1f + 0.05f * Mathf.Sin(Time.realtimeSinceStartup * 6f)));
                }).Every(50);
                return row;
            }

            // Each example ACTS OUT its change: the input grid rotates / flips / slides / inverts until it
            // becomes the output, holds, then resets. Staggered so the rows take turns.
            if (_rule.HasValue && !MotionSettings.ReducedMotion)
            {
                PatternRule rule = _rule.Value;
                void Loop()
                {
                    if (_chosen >= 0 || left.panel == null) return;
                    left.SetMask(input);
                    arrow.SetColor(RonrikuTheme.Muted);
                    left.schedule.Execute(() =>
                    {
                        arrow.SetColor(Palette.Accent);
                        Morph(left, input, output, rule, () => left.schedule.Execute(Loop).StartingIn(1100));
                    }).StartingIn(700);
                }
                left.schedule.Execute(Loop).StartingIn(300 + index * 900);
            }
            return row;
        }

        /// <summary>Animates <paramref name="grid"/> from input to output using the rule's real motion.</summary>
        private static void Morph(PixelGridElement grid, int input, int output, PatternRule rule, System.Action done)
        {
            const float seconds = 0.7f;
            float start = Time.realtimeSinceStartup;
            grid.SetMask(input);
            IVisualElementScheduledItem item = null;
            item = grid.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / seconds);
                float e = t * t * (3f - 2f * t);
                switch (rule)
                {
                    case PatternRule.Rotate90: grid.style.rotate = new Rotate(90f * e); break;
                    case PatternRule.Rotate180: grid.style.rotate = new Rotate(180f * e); break;
                    case PatternRule.Rotate270: grid.style.rotate = new Rotate(-90f * e); break;
                    case PatternRule.MirrorX: grid.style.scale = new Scale(new Vector3(1f - 2f * e, 1f, 1f)); break;
                    case PatternRule.MirrorY: grid.style.scale = new Scale(new Vector3(1f, 1f - 2f * e, 1f)); break;
                    case PatternRule.Transpose:
                        // Transpose = turn a quarter clockwise, then flip left-right.
                        if (e < 0.5f) grid.style.rotate = new Rotate(90f * e * 2f);
                        else
                        {
                            grid.style.rotate = new Rotate(90f);
                            grid.style.scale = new Scale(new Vector3(1f, 1f - 2f * (e - 0.5f) * 2f, 1f));
                        }
                        break;
                    case PatternRule.ShiftRight: grid.style.translate = new Translate(Length.Percent(21f * e), 0); break;
                    case PatternRule.ShiftDown: grid.style.translate = new Translate(0, Length.Percent(21f * e)); break;
                    case PatternRule.Invert: grid.style.opacity = 1f - Mathf.Sin(e * Mathf.PI) * 0.85f; if (e > 0.5f) grid.SetMask(output); break;
                }
                if (t < 1f) return;
                item.Pause();
                grid.style.rotate = new Rotate(0);
                grid.style.scale = new Scale(Vector3.one);
                grid.style.translate = new Translate(0, 0);
                grid.style.opacity = 1f;
                grid.SetMask(output);
                done();
            }).Every(16);
        }
    }
}
