using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>Pattern trial: study the example pairs, then pick the output for the test grid. One answer.</summary>
    public sealed class PatternPuzzleScreen : TrialScreenBase
    {
        private readonly PatternPuzzleData _data;
        private readonly PatternPuzzleValidator _validator = new PatternPuzzleValidator();
        private readonly PixelGridElement[] _optionGrids;
        private int _chosen = -1;

        public PatternPuzzleScreen(PatternPuzzleData data, TrialScreenContext context)
            : base(context, "PATTERN", "FIND THE RULE. PICK THE MISSING OUTPUT", data.Metadata.TimeLimitSeconds,
                data.Metadata.ContentHash)
        {
            _data = data;
            Body.style.justifyContent = Justify.Center;

            float rowHeight = data.Examples.Count > 2 ? 200 : 236;
            foreach (var (input, output) in data.Examples) Body.Add(ExampleRow(input, output, false, rowHeight));
            Body.Add(ExampleRow(data.TestInput, 0, true, rowHeight));

            var options = new VisualElement();
            options.style.flexDirection = FlexDirection.Row;
            options.style.flexWrap = Wrap.Wrap;
            options.style.justifyContent = Justify.SpaceBetween;
            _optionGrids = new PixelGridElement[data.Options.Count];
            for (int i = 0; i < data.Options.Count; i++)
            {
                int index = i;
                var button = new Button(() => Choose(index)) { name = $"option-{i}" };
                button.text = string.Empty;
                button.style.width = Length.Percent(24);
                button.style.height = 220;
                button.style.marginTop = 8;
                button.style.paddingLeft = button.style.paddingRight = 0;
                button.style.paddingTop = button.style.paddingBottom = 0;
                button.style.backgroundColor = Color.clear;
                button.style.borderLeftWidth = button.style.borderRightWidth = 0;
                button.style.borderTopWidth = button.style.borderBottomWidth = 0;
                var grid = new PixelGridElement(data.Options[i], RonrikuTheme.OffWhite);
                grid.style.flexGrow = 1;
                button.Add(grid);
                var letter = UiFactory.Label(((char)('A' + i)).ToString(), 16, RonrikuTheme.Muted, FontStyle.Bold);
                letter.style.height = 32;
                button.Add(letter);
                _optionGrids[i] = grid;
                options.Add(button);
            }
            options.style.marginTop = 16;
            Body.Add(options);

            SetStatus("TAP THE ANSWER. ONE CHOICE", RonrikuTheme.Muted);
            UpdateCounter();
        }

        protected override string CounterPrefix() =>
            $"{_data.Examples.Count} EXAMPLES   //   {_data.Metadata.Difficulty.ToString().ToUpperInvariant()}";

        protected override TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds)
        {
            int target = TrialScoring.TargetMilliseconds("pattern", _data.Metadata.Difficulty);
            int moves = _chosen >= 0 ? 1 : 0;
            int points = TrialScoring.Points(_data.Metadata.Difficulty, solved, elapsedMilliseconds, target, moves, 1, 0, 0);
            return new TrialOutcome(TrialKind.Pattern, _data.Metadata.Difficulty, solved, elapsedMilliseconds, target,
                moves, 0, 0, points);
        }

        private void Choose(int index)
        {
            if (_chosen >= 0) return;
            _chosen = index;
            bool correct = _validator.IsCorrect(_data, index);
            _optionGrids[_data.CorrectOption].SetFrame(RonrikuTheme.Teal);
            if (!correct) _optionGrids[index].SetFrame(RonrikuTheme.Yellow);
            MarkFinished(correct,
                correct ? $"CORRECT   //   {FormatTime(ElapsedMilliseconds)}" : "NOT THIS ONE   //   ANSWER MARKED IN TEAL",
                correct ? RonrikuTheme.Teal : RonrikuTheme.Yellow);
        }

        private static VisualElement ExampleRow(int input, int output, bool isTest, float height)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.height = height;
            row.style.marginTop = 8;

            var left = new PixelGridElement(input, isTest ? RonrikuTheme.Yellow : RonrikuTheme.Teal);
            left.style.width = left.style.height = height - 16;
            row.Add(left);

            var arrow = new PixelLabel(">", RonrikuTheme.Muted, 7);
            arrow.style.width = 120;
            arrow.style.height = 60;
            row.Add(arrow);

            var right = new PixelGridElement(output, RonrikuTheme.Teal, isTest);
            right.style.width = right.style.height = height - 16;
            if (isTest) right.SetFrame(RonrikuTheme.Yellow);
            row.Add(right);
            return row;
        }

        private static string FormatTime(int ms) => $"{ms / 60000:00}:{ms / 1000 % 60:00}";
    }
}
