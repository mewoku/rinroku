using System;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class DailyResultsScreen : VisualElement
    {
        private const float CountUpSeconds = 0.7f;

        private readonly DailyResult _result;
        private readonly Label _rating;
        private readonly float _startedAt;
        private IVisualElementScheduledItem _countUp;

        public DailyResultsScreen(DailyResult result, int dailyNumber, bool localMode, IHapticsService haptics,
            Action home)
        {
            _result = result;
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 48;
            style.paddingBottom = 32;

            var top = new VisualElement();
            top.style.flexGrow = 1;
            Add(top);

            Add(Caption($"DAILY {dailyNumber:000}", RonrikuTheme.Muted, 16, 40));
            var title = new PixelLabel(result.Solved == result.Trials ? "COMPLETE" : "FINISHED", RonrikuTheme.Teal, 10);
            title.style.height = 100;
            Add(title);

            var time = UiFactory.Label(FormatTime(result.ElapsedMilliseconds), 64, RonrikuTheme.OffWhite, FontStyle.Bold);
            time.name = "result-time";
            time.style.height = 96;
            Add(time);
            Add(Caption($"{result.Solved} / {result.Trials} SOLVED   //   {result.Points} PTS", RonrikuTheme.Muted, 17, 40));
            Add(Caption(localMode ? "LOCAL MODE   //   NO GLOBAL PERCENTILE YET" : "PERCENTILE PENDING",
                RonrikuTheme.BlueGrey, 14, 40));

            Add(Rule());

            Add(Caption("REASONING RATING", RonrikuTheme.Muted, 14, 36));
            _rating = UiFactory.Label(string.Empty, 44, RonrikuTheme.OffWhite, FontStyle.Bold);
            _rating.name = "result-rating";
            _rating.style.height = 72;
            Add(_rating);
            string deltaText = !result.Counted ? "PRACTICE   //   NOT RATED"
                : result.RatingDelta >= 0 ? $"+{result.RatingDelta}" : result.RatingDelta.ToString();
            Color deltaColor = !result.Counted ? RonrikuTheme.Muted
                : result.RatingDelta >= 0 ? RonrikuTheme.Teal : RonrikuTheme.Yellow;
            Add(Caption(deltaText, deltaColor, 26, 48));

            var streak = new PixelLabel($"STREAK {result.StreakAfter}", RonrikuTheme.Yellow, 6);
            streak.name = "result-streak";
            streak.style.height = 72;
            streak.style.marginTop = 8;
            Add(streak);

            Add(Rule());

            for (int i = 0; i < result.Outcomes.Count; i++)
            {
                TrialOutcome o = result.Outcomes[i];
                string status = !o.Solved ? (o.Moves > 0 ? "MISSED" : "SKIPPED")
                    : o.Par <= 0 ? "CORRECT"
                    : o.Moves <= o.Par ? "AT PAR" : $"{o.Moves} / {o.Par}";
                Add(Row($"{i + 1}   {o.Kind.ToString().ToUpperInvariant()}  {o.Difficulty.ToString().ToUpperInvariant()}",
                    $"{status}   {FormatTime(o.ElapsedMilliseconds)}", o.Solved ? RonrikuTheme.OffWhite : RonrikuTheme.Muted));
            }

            if (result.Skills != null && result.Skills.Count > 0)
            {
                var skills = new VisualElement();
                skills.style.flexDirection = FlexDirection.Row;
                skills.style.marginTop = 20;
                skills.style.height = 88;
                foreach (SkillChange change in result.Skills)
                {
                    var cell = new VisualElement();
                    cell.style.flexGrow = 1;
                    cell.style.flexBasis = 0;
                    cell.Add(Caption(change.Name.ToUpperInvariant(), RonrikuTheme.Muted, 13, 32));
                    cell.Add(Caption(change.Delta >= 0 ? $"+{change.Delta}" : change.Delta.ToString(),
                        change.Delta >= 0 ? RonrikuTheme.Teal : RonrikuTheme.Yellow, 22, 44));
                    skills.Add(cell);
                }
                Add(skills);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            var homeButton = UiFactory.Button("HOME", () => { haptics.Selection(); home(); }, true);
            homeButton.name = "home-button";
            homeButton.style.height = 88;
            Add(homeButton);

            _rating.text = result.RatingBefore.ToString();
            _startedAt = Time.realtimeSinceStartup;
            bool reducedMotion = PlayerPrefs.GetInt("ronriku.reducedMotion", 0) == 1;
            if (reducedMotion || !result.Counted) ShowFinalRating();
            else _countUp = schedule.Execute(TickRating).Every(16);
            if (result.Counted) haptics.Success();
        }

        private void TickRating()
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _startedAt) / CountUpSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int value = Mathf.RoundToInt(Mathf.Lerp(_result.RatingBefore, _result.RatingAfter, eased));
            _rating.text = $"{_result.RatingBefore}  >  {value}";
            if (t >= 1f) ShowFinalRating();
        }

        private void ShowFinalRating()
        {
            _countUp?.Pause();
            _rating.text = _result.Counted ? $"{_result.RatingBefore}  >  {_result.RatingAfter}" : _result.RatingAfter.ToString();
        }

        private static Label Caption(string text, Color color, int size, int height)
        {
            var label = UiFactory.Label(text, size, color, FontStyle.Bold);
            label.style.height = height;
            return label;
        }

        private static VisualElement Row(string left, string right, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 52;
            var l = UiFactory.Label(left, 17, color, FontStyle.Bold);
            l.style.flexGrow = 1;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            var r = UiFactory.Label(right, 17, color, FontStyle.Bold);
            r.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(l);
            row.Add(r);
            return row;
        }

        private static VisualElement Rule()
        {
            var rule = new VisualElement();
            rule.style.height = 2;
            rule.style.marginTop = rule.style.marginBottom = 20;
            rule.style.backgroundColor = RonrikuTheme.NearBlack;
            return rule;
        }

        private static string FormatTime(int ms) => $"{ms / 60000:00}:{ms / 1000 % 60:00}";
    }
}
