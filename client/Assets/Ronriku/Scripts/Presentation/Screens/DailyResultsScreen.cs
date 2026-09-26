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
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.paddingTop = 16;
            style.paddingBottom = 16;

            var top = new VisualElement();
            top.style.flexGrow = 1;
            Add(top);

            Add(Caption($"DAILY {dailyNumber:000}", RonrikuTheme.Muted, 11, 20));
            var title = new PixelLabel(result.Solved == result.Trials ? "COMPLETE" : "FINISHED", RonrikuTheme.Frost.Accent, 6);
            title.style.height = 56;
            Add(title);

            var time = UiFactory.Label(FormatTime(result.ElapsedMilliseconds), 36, RonrikuTheme.OffWhite, FontStyle.Bold);
            time.name = "result-time";
            time.style.height = 48;
            Add(time);
            Add(Caption($"{result.Solved} / {result.Trials} SOLVED   //   {result.Points} PTS", RonrikuTheme.Muted, 12, 22));
            if (result.ShardsEarned > 0) Add(Caption($"+{result.ShardsEarned} SHARDS", RonrikuTheme.Teal, 14, 24));
            if (localMode) Add(Caption("OFFLINE  //  CONNECT TO RANK", RonrikuTheme.Muted, 10, 20));

            Add(Rule());

            Add(Caption("REASONING RATING", RonrikuTheme.Muted, 10, 18));
            _rating = UiFactory.Label(string.Empty, 26, RonrikuTheme.OffWhite, FontStyle.Bold);
            _rating.name = "result-rating";
            _rating.style.height = 36;
            Add(_rating);
            string deltaText = !result.Counted ? "PRACTICE   //   NOT RATED"
                : result.RatingDelta >= 0 ? $"+{result.RatingDelta}" : result.RatingDelta.ToString();
            Color deltaColor = !result.Counted ? RonrikuTheme.Muted
                : result.RatingDelta >= 0 ? RonrikuTheme.Teal : RonrikuTheme.Yellow;
            Add(Caption(deltaText, deltaColor, 16, 26));

            var streak = new PixelLabel($"STREAK {result.StreakAfter}", RonrikuTheme.Yellow, 3);
            streak.name = "result-streak";
            streak.style.height = 32;
            streak.style.marginTop = 4;
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

            bool skillsDiffer = false;
            if (result.Skills != null)
                foreach (SkillChange c in result.Skills) if (c.Delta != result.Skills[0].Delta) skillsDiffer = true;
            if (skillsDiffer)
            {
                var skills = new VisualElement();
                skills.style.flexDirection = FlexDirection.Row;
                skills.style.marginTop = 10;
                skills.style.height = 48;
                foreach (SkillChange change in result.Skills)
                {
                    var cell = new VisualElement();
                    cell.style.flexGrow = 1;
                    cell.style.flexBasis = 0;
                    cell.Add(Caption(change.Name.ToUpperInvariant(), RonrikuTheme.Muted, 9, 18));
                    cell.Add(Caption(change.Delta >= 0 ? $"+{change.Delta}" : change.Delta.ToString(),
                        change.Delta >= 0 ? RonrikuTheme.Teal : RonrikuTheme.Yellow, 14, 24));
                    skills.Add(cell);
                }
                Add(skills);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            var buttons = UiFactory.Row();
            var shareButton = UiFactory.FlatButton("SHARE", () =>
            {
                string text = Share.DailyText(result, dailyNumber, RonrikuTuning.Current.shareUrl);
                if (!Share.Send(text)) Toast.Show(this, "COPIED  ·  PASTE IT ANYWHERE", RonrikuTheme.Teal);
            });
            shareButton.name = "share-button";
            shareButton.style.width = 120;
            shareButton.style.height = 52;
            buttons.Add(shareButton);
            var homeButton = UiFactory.GlowButton("CONTINUE", () => { haptics.Selection(); home(); }, RonrikuTheme.Frost);
            homeButton.name = "home-button";
            homeButton.style.height = 52;
            homeButton.style.flexGrow = 1;
            homeButton.style.marginLeft = 10;
            buttons.Add(homeButton);
            Add(buttons);

            _rating.text = result.RatingBefore.ToString();
            _startedAt = Time.realtimeSinceStartup;
            bool reducedMotion = PlayerPrefs.GetInt("ronriku.reducedMotion", 0) == 1;
            if (reducedMotion || !result.Counted) ShowFinalRating();
            else _countUp = schedule.Execute(TickRating).Every(16);
            if (result.Counted) Accessibility.Feedback.Win();
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
            row.style.height = 28;
            var l = UiFactory.Label(left, 11, color, FontStyle.Bold);
            l.style.flexGrow = 1;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            var r = UiFactory.Label(right, 11, color, FontStyle.Bold);
            r.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(l);
            row.Add(r);
            return row;
        }

        private static VisualElement Rule()
        {
            var rule = new VisualElement();
            rule.style.height = 2;
            rule.style.marginTop = rule.style.marginBottom = 10;
            rule.style.backgroundColor = RonrikuTheme.Line;
            return rule;
        }

        private static string FormatTime(int ms) => $"{ms / 60000:00}:{ms / 1000 % 60:00}";
    }
}
