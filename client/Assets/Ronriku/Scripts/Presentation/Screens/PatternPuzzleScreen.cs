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

        public PatternPuzzleScreen(PatternPuzzleData data, TrialScreenContext context)
            : base(context, "PATTERN", "SAME CHANGE. WHICH ONE COMES OUT?", data.Metadata.TimeLimitSeconds,
                data.Metadata.ContentHash)
        {
            _data = data;
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

            SetStatus("TAP THE MISSING PIECE", RonrikuTheme.Muted);
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
        }

        private VisualElement ExampleRow(int input, int output, bool isTest, float size, int index)
        {
            var row = UiFactory.Row();
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 8;

            var left = new PixelGridElement(input, isTest ? RonrikuTheme.Yellow : Palette.Accent);
            left.style.width = left.style.height = size;
            row.Add(left);

            var arrow = new PixelIcon("play", RonrikuTheme.Muted, 20);
            arrow.style.marginLeft = arrow.style.marginRight = 18;
            row.Add(arrow);

            var right = new PixelGridElement(isTest ? 0 : output, Palette.Accent2 == default ? Palette.Accent : Color.Lerp(Palette.Accent, Palette.Accent2, 0.35f), isTest);
            right.style.width = right.style.height = size;
            if (isTest) right.SetFrame(RonrikuTheme.Yellow);
            row.Add(right);

            // Loop: the output scans in cell by cell from the input, and the arrow brightens, so the change is visible.
            float phase = index * 0.45f;
            row.schedule.Execute(() =>
            {
                if (_chosen >= 0 || MotionSettings.ReducedMotion) return;
                float t = Mathf.Repeat(Time.realtimeSinceStartup * 0.55f + phase, 1f);
                arrow.SetColor(t < 0.35f ? Palette.Accent : RonrikuTheme.Muted);
                if (isTest)
                {
                    right.style.scale = new Scale(Vector3.one * (1f + 0.05f * Mathf.Sin(Time.realtimeSinceStartup * 6f)));
                    return;
                }
                // Input and output pulse in turn, reading left → right.
                left.style.scale = new Scale(Vector3.one * (t < 0.3f ? 1.06f : 1f));
                right.style.scale = new Scale(Vector3.one * (t >= 0.35f && t < 0.65f ? 1.06f : 1f));
            }).Every(50);
            return row;
        }
    }
}
