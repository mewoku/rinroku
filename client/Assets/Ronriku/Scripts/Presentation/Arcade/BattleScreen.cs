using System;
using System.Text;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    /// <summary>
    /// Battle: the hero fights a monster by answering a stream of micro-challenges. Fast right answers
    /// hit hard (chips × combo mult), the monster swings on a timer, wrong answers hurt. Everything
    /// shakes, pops and counts up.
    /// </summary>
    public sealed class BattleScreen : VisualElement
    {
        private static int ResolveRightMs => RonrikuTuning.Current.resolveRightMs;
        private static int ResolveWrongMs => RonrikuTuning.Current.resolveWrongMs;
        private static int StaggerMs => RonrikuTuning.Current.staggerMs;

        private readonly BattleState _state;
        private readonly Palette _palette;
        private readonly Action<ArcadeResult> _completed;
        private readonly Arena _arena;
        private readonly HpBar _hp;
        private readonly HeartsRow _hearts;
        private readonly ChipsMult _score;
        private readonly Label _combo;
        private readonly VisualElement _swingFill;
        private readonly VisualElement _cardSlot;
        private readonly VisualElement _overlay;
        private readonly float _startedAt;
        private ChallengeView _view;
        private float _challengeStart;
        private float _showingSince = -1f;
        private float _shownMs;
        private float _swingMs;
        private float _lastTick;
        private bool _active;
        private bool _finished;
        private int _totalDamage;

        public BattleState State => _state;

        public BattleScreen(BattleConfig config, Figure hero, Figure monster, Palette palette, string title,
            Action back, Action<ArcadeResult> completed)
        {
            _state = new BattleState(config);
            _palette = palette;
            _completed = completed;
            this.name = "battle";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.paddingTop = 6;
            style.paddingBottom = 12;

            var top = UiFactory.Row();
            top.style.height = 48;
            top.style.flexShrink = 0;
            var backButton = UiFactory.FlatButton(string.Empty, back);
            backButton.name = "back-button";
            backButton.style.width = 44;
            backButton.style.height = 40;
            backButton.style.alignItems = Align.Center;
            backButton.style.justifyContent = Justify.Center;
            backButton.Add(new PixelIcon("back", RonrikuTheme.Text, 16));
            top.Add(backButton);
            var titleLabel = UiFactory.Heading(title, 11, RonrikuTheme.Muted);
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.marginLeft = 10;
            top.Add(titleLabel);
            _hearts = new HeartsRow(config.Hearts);
            _hearts.Set(config.Hearts);
            top.Add(_hearts);
            Add(top);

            var monsterName = UiFactory.Heading(monster?.Name ?? "MONSTER", 13, palette.Accent);
            monsterName.style.height = 20;
            monsterName.style.flexShrink = 0;
            Add(monsterName);
            _hp = new HpBar(config.MonsterHp, RonrikuTheme.Red) { name = "monster-hp" };
            _hp.style.flexShrink = 0;
            Add(_hp);

            _arena = new Arena(hero, monster, palette, 150);
            Add(_arena);

            var swing = new VisualElement();
            swing.style.height = 8;
            swing.style.flexShrink = 0;
            swing.style.backgroundColor = RonrikuTheme.NearBlack;
            swing.style.marginTop = 4;
            _swingFill = new VisualElement();
            _swingFill.style.height = Length.Percent(100);
            _swingFill.style.width = 0;
            _swingFill.style.backgroundColor = RonrikuTheme.Yellow;
            swing.Add(_swingFill);
            Add(swing);

            var scoreRow = UiFactory.Row();
            scoreRow.style.height = 54;
            scoreRow.style.flexShrink = 0;
            scoreRow.style.justifyContent = Justify.SpaceBetween;
            _combo = UiFactory.Heading("COMBO 0", 13, RonrikuTheme.Muted);
            _combo.style.width = 110;
            _combo.style.unityTextAlign = TextAnchor.MiddleLeft;
            scoreRow.Add(_combo);
            _score = new ChipsMult();
            scoreRow.Add(_score);
            Add(scoreRow);

            _cardSlot = new VisualElement { name = "card-slot" };
            _cardSlot.style.flexGrow = 1;
            _cardSlot.style.overflow = Overflow.Hidden;
            Add(_cardSlot);

            _overlay = new VisualElement { pickingMode = PickingMode.Ignore, name = "fx" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.right = _overlay.style.top = _overlay.style.bottom = 0;
            Add(_overlay);

            _startedAt = Time.realtimeSinceStartup;
            _lastTick = _startedAt;
            schedule.Execute(Tick).Every(33);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Juice.Banner(_overlay, "FIGHT!", palette.Accent, 0.8f, ShowChallenge);
                Feedback.Whoosh();
            });
        }

        private void ShowChallenge()
        {
            if (_finished) return;
            _cardSlot.Clear();
            _view = new ChallengeView(_state.Current, _palette);
            _view.Answered += OnAnswered;
            _view.Showing += showing =>
            {
                if (showing) _showingSince = Time.realtimeSinceStartup;
                else if (_showingSince >= 0)
                {
                    _shownMs += (Time.realtimeSinceStartup - _showingSince) * 1000f;
                    _showingSince = -1f;
                }
            };
            _cardSlot.Add(_view);
            Juice.SlideIn(_view);
            _challengeStart = Time.realtimeSinceStartup;
            _shownMs = 0;
            _active = true;
            _score.Set(0, Math.Min(BattleState.MaxMult, 1 + _state.Combo));
        }

        private void Tick()
        {
            float now = Time.realtimeSinceStartup;
            float dt = (now - _lastTick) * 1000f;
            _lastTick = now;
            if (!_active || _finished || _showingSince >= 0) return;
            _swingMs += dt;
            float t = Mathf.Clamp01(_swingMs / _state.Config.AttackMs);
            _swingFill.style.width = Length.Percent(t * 100f);
            _swingFill.style.backgroundColor = t > 0.8f ? RonrikuTheme.Red : Color.Lerp(RonrikuTheme.Yellow, RonrikuTheme.Hex("FF8A3D"), t);
            if (t > 0.8f && Mathf.Repeat(now, 0.25f) < 0.034f) Juice.Punch(_arena.Monster, 0.05f, 0.12f);
            if (_swingMs >= _state.Config.AttackMs) MonsterSwing();
        }

        private void MonsterSwing()
        {
            _swingMs = 0;
            int before = _state.Hearts;
            _state.MonsterSwing();
            _arena.MonsterAttack();
            _hearts.Set(_state.Hearts);
            _hearts.Break(before - 1);
            Feedback.Hit();
            Juice.Shake(this, 12f, 0.35f);
            Juice.Flash(_overlay, RonrikuTheme.WithAlpha(RonrikuTheme.Red, 0.35f), 0.3f);
            Juice.Popup(_overlay, _arena.HeroCenter(_overlay), "OUCH", RonrikuTheme.Red, 22);
            SetCombo();
            if (_state.Lost) Finish();
        }

        private void OnAnswered(int answer)
        {
            if (_finished) return;
            _active = false;
            float thinkMs = (Time.realtimeSinceStartup - _challengeStart) * 1000f - _shownMs;
            HitResult hit = _state.Answer(answer, Mathf.Max(0, Mathf.RoundToInt(thinkMs)));
            _view.Reveal(hit.Correct, answer);
            if (hit.Correct) Strike(hit);
            else Miss();
            schedule.Execute(() =>
            {
                if (_state.Over) Finish();
                else ShowChallenge();
            }).StartingIn(hit.Correct ? ResolveRightMs : ResolveWrongMs);
        }

        private void Strike(HitResult hit)
        {
            _totalDamage += hit.Damage;
            _swingMs = Mathf.Max(0, _swingMs - StaggerMs);
            _score.PunchChips(hit.Chips + hit.SpeedBonus);
            schedule.Execute(() => _score.PunchMult(hit.Mult)).StartingIn(110);
            if (hit.SpeedBonus >= 7) Juice.Popup(_overlay, _overlay.WorldToLocal(_score.worldBound.center) + new Vector2(-40, -10), "FAST!", RonrikuTheme.Teal, 16, 40f, 0.7f);
            schedule.Execute(() =>
            {
                _arena.HeroAttack();
                Feedback.Hit();
                int size = Mathf.Clamp(22 + hit.Damage / 6, 22, 48);
                Color color = hit.Mult >= 4 ? RonrikuTheme.Gold : hit.Mult >= 2 ? RonrikuTheme.Hex("FF8A3D") : RonrikuTheme.Text;
                Juice.Popup(_overlay, _arena.MonsterCenter(_overlay), "-" + hit.Damage, color, size, 80f);
                _hp.Set(_state.Hp);
                Juice.Shake(this, Mathf.Min(3f + hit.Damage / 18f, 14f), 0.25f);
            }).StartingIn(230);
            SetCombo();
        }

        private void Miss()
        {
            int before = _state.Hearts + 1;
            Feedback.Error();
            schedule.Execute(() =>
            {
                _arena.MonsterAttack();
                _hearts.Set(_state.Hearts);
                _hearts.Break(before - 1);
                Feedback.Hit();
                Juice.Shake(this, 12f, 0.35f);
                Juice.Flash(_overlay, RonrikuTheme.WithAlpha(RonrikuTheme.Red, 0.35f), 0.3f);
            }).StartingIn(250);
            _score.Set(0, 1);
            SetCombo();
        }

        private void SetCombo()
        {
            int combo = _state.Combo;
            _combo.text = combo > 0 ? $"COMBO {combo}" : "COMBO 0";
            _combo.style.color = combo >= 5 ? RonrikuTheme.Gold : combo >= 3 ? RonrikuTheme.Hex("FF8A3D") : combo > 0 ? _palette.Accent : RonrikuTheme.Muted;
            _combo.style.fontSize = 13 + Math.Min(combo, 5);
            if (combo > 0) Juice.Punch(_combo, 0.3f);
            ComboChanged?.Invoke(combo);
        }

        /// <summary>Raised whenever the combo changes (music intensity hooks into this).</summary>
        public event Action<int> ComboChanged;

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            _active = false;
            var result = new ArcadeResult
            {
                Won = _state.Won,
                Stars = _state.Stars,
                ElapsedMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f),
                Score = _totalDamage,
                BestCombo = _state.BestCombo,
                Proof = Proof(_state)
            };
            if (_state.Won)
            {
                _arena.MonsterDies(_palette);
                Feedback.Win();
                Juice.Banner(_overlay, "VICTORY", RonrikuTheme.Gold, 1.2f, () => _completed(result));
            }
            else
            {
                Feedback.Lose();
                Juice.Banner(_overlay, "DEFEATED", RonrikuTheme.Red, 1.2f, () => _completed(result));
            }
        }

        public static string Proof(BattleState state)
        {
            var sb = new StringBuilder("B1:");
            foreach (var (answer, ms) in state.Log) sb.Append(answer).Append('@').Append(ms).Append(',');
            sb.Append("s").Append(state.MonsterSwings);
            return sb.ToString();
        }
    }
}
