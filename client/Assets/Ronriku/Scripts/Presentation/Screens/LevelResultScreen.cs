using System;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class LevelResultModel
    {
        public bool Won;
        public bool Boss;
        public string Title;
        public int Stars;
        public int ShardsEarned;
        public int ElapsedMs;
        public Figure Monster;
        public Palette Palette;
        public string NextLabel = "CONTINUE";
    }

    /// <summary>Level / boss outcome: defeated guardian, stars popping in, shard count-up.</summary>
    public sealed class LevelResultScreen : VisualElement
    {
        private readonly Label _shards;
        private readonly LevelResultModel _model;
        private readonly float _start;

        public LevelResultScreen(LevelResultModel model, Action next, Action retry)
        {
            _model = model;
            name = "level-result";
            style.flexGrow = 1;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Stretch;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter * 1.5f;

            var tag = UiFactory.Heading(model.Boss ? "BOSS" : "LEVEL", 11, RonrikuTheme.Muted);
            Add(tag);
            var title = new PixelLabel(model.Won ? (model.Boss ? "VICTORY" : "CLEAR") : "DEFEAT",
                model.Won ? model.Palette.Accent : RonrikuTheme.Red, 6);
            title.style.height = 56;
            Add(title);
            var sub = UiFactory.Heading(model.Title, 14, RonrikuTheme.Text);
            Add(sub);

            if (model.Monster != null)
            {
                var monster = new VoxelView(model.Monster, 80, model.Won ? 180f : 20f, false);
                monster.style.width = monster.style.height = 170;
                monster.style.alignSelf = Align.Center;
                monster.style.marginTop = 8;
                if (model.Won)
                {
                    float start = Time.realtimeSinceStartup;
                    monster.schedule.Execute(() =>
                    {
                        float t = Time.realtimeSinceStartup - start;
                        float k = Mathf.Clamp01((t - 0.3f) / 0.6f);
                        monster.style.scale = new Scale(Vector3.one * (1f - 0.6f * k));
                        monster.style.opacity = 1f - 0.75f * k;
                        monster.style.rotate = new Rotate(new Angle(k * 18f));
                    }).Every(16);
                }
                Add(monster);
            }

            var stars = UiFactory.Row();
            stars.style.justifyContent = Justify.Center;
            stars.style.marginTop = 8;
            for (int i = 0; i < 3; i++)
            {
                var star = new PixelIcon("star", RonrikuTheme.Line, 40) { name = $"result-star-{i}" };
                star.style.marginLeft = star.style.marginRight = 8;
                stars.Add(star);
                if (!model.Won || i >= model.Stars) continue;
                int index = i;
                star.schedule.Execute(() =>
                {
                    star.SetColor(RonrikuTheme.Yellow);
                    star.style.scale = new Scale(Vector3.one * 1.35f);
                    Feedback.Coin();
                    star.schedule.Execute(() => star.style.scale = new Scale(Vector3.one)).StartingIn(120);
                }).StartingIn(350 + index * 260);
            }
            Add(stars);

            var reward = UiFactory.Row();
            reward.style.justifyContent = Justify.Center;
            reward.style.marginTop = 14;
            reward.Add(new PixelIcon("shard", RonrikuTheme.Teal, 20));
            _shards = UiFactory.Heading("+0", 22, RonrikuTheme.Teal);
            _shards.name = "result-shards";
            _shards.style.marginLeft = 8;
            reward.Add(_shards);
            Add(reward);
            var time = UiFactory.Label($"{model.ElapsedMs / 60000:00}:{model.ElapsedMs / 1000 % 60:00}", 12, RonrikuTheme.Muted);
            time.style.marginTop = 4;
            Add(time);

            var go = UiFactory.GlowButton(model.NextLabel, next, model.Palette);
            go.name = "result-continue";
            go.style.marginTop = 24;
            Add(go);
            if (retry != null)
            {
                var again = UiFactory.FlatButton(model.Won ? "REPLAY FOR STARS" : "TRY AGAIN", retry);
                again.name = "result-retry";
                again.style.marginTop = 10;
                Add(again);
            }

            _start = Time.realtimeSinceStartup;
            schedule.Execute(CountUp).Every(16);
            if (model.Won) Feedback.Win(); else Feedback.Lose();
        }

        private void CountUp()
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _start - 0.9f) / 0.7f);
            _shards.text = $"+{Mathf.RoundToInt(_model.ShardsEarned * (1f - Mathf.Pow(1f - t, 3f)))}";
        }
    }
}
