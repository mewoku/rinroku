using System;
using System.Collections.Generic;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    public static class SuitStyle
    {
        public static Color Color(Suit suit) => suit switch
        {
            Suit.Fire => RonrikuTheme.Hex("FF5A3D"),
            Suit.Wave => RonrikuTheme.Hex("3A9BFF"),
            Suit.Leaf => RonrikuTheme.Hex("7BE35A"),
            _ => RonrikuTheme.Hex("FFD23D")
        };

        public static int Glyph(Suit suit) => suit switch
        {
            Suit.Fire => Token.Triangle,
            Suit.Wave => Token.Circle,
            Suit.Leaf => Token.Diamond,
            _ => Token.Arrow
        };

        public static string Name(Suit suit) => suit.ToString().ToUpperInvariant();
    }

    /// <summary>A rune card: big number, sign glyph, lifts when selected.</summary>
    public sealed class CardElement : VisualElement
    {
        private bool _selected;
        public Card Card { get; }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                style.translate = new Translate(0, value ? -18 : 0);
                UiFactory.SetBorder(this, 3, value ? RonrikuTheme.Text : Color.Lerp(SuitStyle.Color(Card.Suit), Color.black, 0.3f));
            }
        }

        public CardElement(Card card, float width = 50, float height = 72)
        {
            Card = card;
            style.width = width;
            style.height = height;
            style.flexShrink = 0;
            style.marginLeft = style.marginRight = 2;
            Color color = SuitStyle.Color(card.Suit);
            style.backgroundImage = new StyleBackground(PixelTextures.VerticalGradient(Color.Lerp(color, RonrikuTheme.Surface2, 0.72f), RonrikuTheme.Surface));
            style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            style.alignItems = Align.Center;
            var value = UiFactory.Heading(card.Value.ToString(), (int)(height * 0.36f), RonrikuTheme.Text);
            value.style.height = height * 0.5f;
            value.style.marginTop = 2;
            value.style.unityTextOutlineColor = RonrikuTheme.Background;
            value.style.unityTextOutlineWidth = 1f;
            Add(value);
            var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
            glyph.style.width = glyph.style.height = height * 0.34f;
            glyph.generateVisualContent += ctx =>
                Glyphs.Shape(ctx.painter2D, SuitStyle.Glyph(card.Suit), glyph.contentRect.center, glyph.contentRect.width * 0.42f, color, 0);
            Add(glyph);
            Selected = false;
        }
    }

    /// <summary>
    /// Rune Hand: pick up to five runes, see the combo and chips × mult before you commit, then watch it
    /// score card by card. Beat the monster's HP within four hands; discards swap out bad runes.
    /// </summary>
    public sealed class CardsScreen : VisualElement
    {
        private const int StepMs = 170;

        private readonly CardsState _state;
        private readonly Palette _palette;
        private readonly Action<ArcadeResult> _completed;
        private readonly Arena _arena;
        private readonly HpBar _hp;
        private readonly Label _counts;
        private readonly Label _comboName;
        private readonly ChipsMult _score;
        private readonly VisualElement _playArea;
        private readonly VisualElement _handRow;
        private readonly VisualElement _overlay;
        private readonly Button _play;
        private readonly Button _discard;
        private readonly float _startedAt;
        private readonly List<CardElement> _cards = new List<CardElement>();
        private bool _busy;
        private bool _finished;
        private ArcadeResult _result;
        private bool _reported;

        public CardsState State => _state;
        /// <summary>True while a played hand is being scored.</summary>
        public bool Busy => _busy;

        public CardsScreen(CardsConfig config, Figure hero, Figure monster, Palette palette, string title,
            Action back, Action<ArcadeResult> completed)
        {
            _state = new CardsState(config);
            _palette = palette;
            _completed = completed;
            this.name = "cards";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = 12;
            style.paddingTop = 6;
            style.paddingBottom = 12;

            var top = UiFactory.Row();
            top.style.height = 48;
            top.style.flexShrink = 0;
            var backButton = UiFactory.FlatButton(string.Empty, () =>
            {
                if (_state.Over) { Finish(); CompleteNow(); }
                else back();
            });
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
            _counts = UiFactory.Heading(string.Empty, 11, RonrikuTheme.Text);
            top.Add(_counts);
            Add(top);

            var monsterName = UiFactory.Heading(monster?.Name ?? "MONSTER", 13, palette.Accent);
            monsterName.style.height = 20;
            monsterName.style.flexShrink = 0;
            Add(monsterName);
            _hp = new HpBar(config.Target, RonrikuTheme.Red) { name = "monster-hp" };
            _hp.style.flexShrink = 0;
            Add(_hp);

            _arena = new Arena(hero, monster, palette, 118);
            Add(_arena);

            if (config.Charms.Length > 0)
            {
                var charms = UiFactory.Row();
                charms.style.justifyContent = Justify.Center;
                charms.style.flexShrink = 0;
                charms.style.marginTop = 4;
                foreach (CharmId charm in config.Charms)
                {
                    var chip = UiFactory.FlatButton(Charms.Name(charm), () => Toast.Show(this, Charms.Describe(charm), RonrikuTheme.Gold));
                    chip.style.height = 30;
                    chip.style.fontSize = 10;
                    chip.style.color = RonrikuTheme.Gold;
                    chip.style.marginLeft = chip.style.marginRight = 3;
                    UiFactory.SetBorder(chip, 2, RonrikuTheme.WithAlpha(RonrikuTheme.Gold, 0.6f));
                    charms.Add(chip);
                }
                Add(charms);
            }

            // Middle stage: played runes land here, then the combo and chips x mult read out under them.
            Add(UiFactory.Spacer());
            _playArea = UiFactory.Row();
            _playArea.name = "play-area";
            _playArea.style.justifyContent = Justify.Center;
            _playArea.style.height = 110;
            _playArea.style.flexShrink = 0;
            Add(_playArea);
            _comboName = UiFactory.Heading("PICK UP TO 5 RUNES", 14, RonrikuTheme.Muted);
            _comboName.name = "combo-name";
            _comboName.style.height = 28;
            _comboName.style.marginTop = 8;
            _comboName.style.flexShrink = 0;
            Add(_comboName);
            _score = new ChipsMult();
            _score.style.flexShrink = 0;
            Add(_score);
            Add(UiFactory.Spacer());
            _handRow = UiFactory.Row();
            _handRow.name = "hand";
            _handRow.style.justifyContent = Justify.Center;
            _handRow.style.height = 100;
            _handRow.style.flexShrink = 0;
            _handRow.style.alignItems = Align.FlexEnd;
            Add(_handRow);

            var buttons = UiFactory.Row();
            buttons.style.flexShrink = 0;
            buttons.style.marginTop = 8;
            _discard = UiFactory.FlatButton("DISCARD", Discard);
            _discard.name = "discard";
            _discard.style.width = 118;
            buttons.Add(_discard);
            var hint = UiFactory.FlatButton("HINT", Hint);
            hint.name = "hint";
            hint.style.width = 76;
            hint.style.marginLeft = 8;
            buttons.Add(hint);
            _play = UiFactory.GlowButton("PLAY", Play, palette);
            _play.name = "play";
            _play.style.flexGrow = 1;
            _play.style.marginLeft = 8;
            buttons.Add(_play);
            Add(buttons);

            _overlay = new VisualElement { pickingMode = PickingMode.Ignore, name = "fx" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.right = _overlay.style.top = _overlay.style.bottom = 0;
            Add(_overlay);

            _startedAt = Time.realtimeSinceStartup;
            RenderHand();
            RefreshPreview();
            RegisterCallback<AttachToPanelEvent>(_ => Juice.Banner(_overlay, "DEAL!", palette.Accent, 0.7f));
        }

        private int SelectedMask()
        {
            int mask = 0;
            for (int i = 0; i < _cards.Count; i++) if (_cards[i].Selected) mask |= 1 << i;
            return mask;
        }

        private void RenderHand()
        {
            _handRow.Clear();
            _cards.Clear();
            float width = Mathf.Min(52f, 400f / Mathf.Max(1, _state.Hand.Count) - 4);
            for (int i = 0; i < _state.Hand.Count; i++)
            {
                var card = new CardElement(_state.Hand[i], width, width * 1.45f) { name = $"card-{i}" };
                int index = i;
                card.RegisterCallback<PointerDownEvent>(_ => Toggle(index));
                _cards.Add(card);
                _handRow.Add(card);
            }
            _counts.text = $"HANDS {_state.HandsLeft}  ·  DISCARDS {_state.DiscardsLeft}";
            _discard.SetEnabled(_state.DiscardsLeft > 0);
        }

        public void Toggle(int index)
        {
            if (_busy || _finished || index >= _cards.Count) return;
            var card = _cards[index];
            if (!card.Selected && Challenges.PopCount(SelectedMask()) >= CardsState.MaxPlay)
            {
                Juice.Shake(card, 5f, 0.2f);
                Feedback.Error();
                return;
            }
            card.Selected = !card.Selected;
            Feedback.Snap();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            int mask = SelectedMask();
            ScoreBreakdown preview = mask != 0 ? _state.Preview(mask) : null;
            if (preview == null)
            {
                _comboName.text = "PICK UP TO 5 RUNES";
                _comboName.style.color = RonrikuTheme.Muted;
                _score.Set(0, 0);
                _play.SetEnabled(false);
                return;
            }
            _comboName.text = $"{HandEvaluator.Name(preview.Combo)}  ·  {preview.Total}";
            _comboName.style.color = preview.Total >= _state.Config.Target - _state.Score ? RonrikuTheme.Gold : _palette.Accent;
            _score.Set(preview.Chips, preview.Mult);
            _play.SetEnabled(true);
        }

        private void Hint()
        {
            if (_busy || _finished) return;
            var (mask, _) = _state.BestPlay();
            for (int i = 0; i < _cards.Count; i++) _cards[i].Selected = (mask & (1 << i)) != 0;
            Feedback.Snap();
            RefreshPreview();
        }

        private void Discard()
        {
            int mask = SelectedMask();
            if (_busy || mask == 0 || !_state.Discard(mask))
            {
                Feedback.Error();
                return;
            }
            Feedback.Whoosh();
            RenderHand();
            for (int i = 0; i < _cards.Count; i++) Juice.SlideIn(_cards[i], 0, 0.01f);
            RefreshPreview();
        }

        public void Play()
        {
            int mask = SelectedMask();
            if (_busy || _finished || mask == 0) return;
            var playedCards = new List<Card>();
            for (int i = 0; i < _cards.Count; i++) if ((mask & (1 << i)) != 0) playedCards.Add(_cards[i].Card);
            int scoreBefore = _state.Score;
            ScoreBreakdown breakdown = _state.Play(mask);
            if (breakdown == null) return;
            _busy = true;
            _play.SetEnabled(false);

            // Played cards move into the play area; the rest of the hand stays put until the score lands.
            _playArea.Clear();
            var shown = new List<CardElement>();
            foreach (Card card in playedCards)
            {
                var element = new CardElement(card, 66, 96);
                _playArea.Add(element);
                shown.Add(element);
                Juice.SlideIn(element, 0, 0.01f);
            }
            for (int i = _cards.Count - 1; i >= 0; i--) if ((mask & (1 << i)) != 0) _cards[i].RemoveFromHierarchy();
            Feedback.Whoosh();

            int chips = 0, mult = 0, delay = 250;
            foreach (ScoreStep step in breakdown.Steps)
            {
                ScoreStep s = step;
                schedule.Execute(() =>
                {
                    if (s.CardIndex >= 0)
                    {
                        chips += s.Chips;
                        mult += s.Mult;
                        var card = shown[s.CardIndex];
                        Juice.Punch(card, 0.3f);
                        card.style.translate = new Translate(0, -10);
                        Vector2 at = _overlay.WorldToLocal(card.worldBound.center) + new Vector2(0, -78);
                        Juice.Popup(_overlay, at, "+" + s.Chips, ChipsMult.ChipsColor, 18, 30f, 0.6f);
                        if (s.Mult > 0) Juice.Popup(_overlay, at + new Vector2(0, -24), "+" + s.Mult + " MULT", ChipsMult.MultColor, 14, 30f, 0.7f);
                        _score.PunchChips(chips);
                        if (s.Mult > 0) _score.PunchMult(mult);
                        Feedback.Coin();
                    }
                    else if (s.Charm.HasValue)
                    {
                        chips += s.Chips;
                        if (s.Times > 1) mult *= s.Times;
                        else mult += s.Mult;
                        string text = s.Times > 1 ? $"{s.Text}  X{s.Times}" : s.Chips > 0 ? $"{s.Text}  +{s.Chips} +{s.Mult}" : $"{s.Text}  +{s.Mult} MULT";
                        Juice.Popup(_overlay, _overlay.WorldToLocal(_score.worldBound.center) + new Vector2(0, 46), text, RonrikuTheme.Gold, 15, 36f, 0.8f);
                        _score.PunchChips(chips);
                        _score.PunchMult(mult);
                        Feedback.Snap();
                    }
                    else
                    {
                        chips = s.Chips;
                        mult = s.Mult;
                        _comboName.text = s.Text;
                        _comboName.style.color = _palette.Accent;
                        Juice.Punch(_comboName, 0.35f);
                        _score.PunchChips(chips);
                        _score.PunchMult(mult);
                        Feedback.Snap();
                    }
                }).StartingIn(delay);
                delay += StepMs;
            }

            delay += 120;
            schedule.Execute(() =>
            {
                int total = breakdown.Total;
                bool huge = total >= _state.Config.Target / 3;
                _comboName.text = $"{breakdown.Chips} X {breakdown.Mult} = {total}";
                _comboName.style.color = huge ? RonrikuTheme.Gold : RonrikuTheme.Text;
                Juice.Punch(_comboName, 0.5f, 0.3f);
                _arena.HeroAttack();
                Feedback.Hit();
                Juice.Popup(_overlay, _arena.MonsterCenter(_overlay), "-" + total, huge ? RonrikuTheme.Gold : RonrikuTheme.Text,
                    Mathf.Clamp(24 + total / 12, 24, 54), 80f, 1f);
                if (huge)
                {
                    Juice.Flash(_overlay, RonrikuTheme.WithAlpha(RonrikuTheme.Hex("FF8A3D"), 0.35f), 0.35f);
                    Juice.Banner(_overlay, "ON FIRE", RonrikuTheme.Hex("FF8A3D"), 0.7f);
                }
                Juice.Shake(this, Mathf.Min(4f + total / 25f, 16f), 0.3f);
                _hp.Set(Mathf.Max(0, _state.Config.Target - _state.Score));
                ScoreLanded?.Invoke(total, scoreBefore);
            }).StartingIn(delay);

            schedule.Execute(() =>
            {
                _playArea.Clear();
                _busy = false;
                if (_state.Over) Finish();
                else
                {
                    RenderHand();
                    RefreshPreview();
                }
            }).StartingIn(delay + 850);
        }

        /// <summary>Raised when a hand's total hits the monster (music stingers hook in).</summary>
        public event Action<int, int> ScoreLanded;

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            var result = _result = new ArcadeResult
            {
                Won = _state.Won,
                Stars = _state.Stars,
                ElapsedMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f),
                Score = _state.Score,
                Proof = "C1:h" + _state.Config.Heat + "|" + string.Join(",", Array.ConvertAll(_state.Config.Charms, c => ((int)c).ToString())) + "|" + string.Join(",", _state.Log)
            };
            if (_state.Won)
            {
                _arena.MonsterDies(_palette);
                Feedback.Win();
                Juice.Banner(_overlay, "VICTORY", RonrikuTheme.Gold, 1.2f, CompleteNow);
            }
            else
            {
                Feedback.Lose();
                _arena.MonsterAttack();
                Juice.Banner(_overlay, "OUT OF HANDS", RonrikuTheme.Red, 1.3f, CompleteNow);
            }
        }

        private void CompleteNow()
        {
            if (_reported || _result == null) return;
            _reported = true;
            _completed(_result);
        }
    }

    /// <summary>Pick a charm (or two for bosses) before a card fight. Big cards, one tap each.</summary>
    public sealed class CharmPickScreen : VisualElement
    {
        private readonly List<CharmId> _picked = new List<CharmId>();
        private bool _committed;

        public CharmPickScreen(CharmId[] offer, int picks, Palette palette, string title, Action back, Action<CharmId[]> done,
            Figure monster = null, string stakes = null)
        {
            name = "charm-pick";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.paddingTop = 6;
            style.paddingBottom = 16;

            var top = UiFactory.Row();
            top.style.height = 48;
            var backButton = UiFactory.FlatButton(string.Empty, back);
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
            Add(top);

            var heading = new PixelLabel(picks > 1 ? "PICK 2 CHARMS" : "PICK A CHARM", RonrikuTheme.Gold, 4);
            heading.style.height = 44;
            heading.style.marginTop = 10;
            Add(heading);
            var sub = UiFactory.Paragraph("CHARMS BEND THE RULES FOR THIS FIGHT. RUNES SCORE CHIPS X MULT.", 12, RonrikuTheme.Muted);
            sub.style.marginBottom = 14;
            Add(sub);

            for (int i = 0; i < offer.Length; i++)
            {
                CharmId charm = offer[i];
                var card = new Button { name = $"charm-{i}", text = string.Empty };
                card.style.height = 108;
                card.style.marginLeft = card.style.marginRight = 0;
                card.style.marginBottom = 12;
                card.style.backgroundImage = new StyleBackground(PixelTextures.DiagonalGradient(RonrikuTheme.WithAlpha(RonrikuTheme.Gold, 0.18f), RonrikuTheme.Surface));
                card.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                UiFactory.SetBorder(card, 3, RonrikuTheme.WithAlpha(RonrikuTheme.Gold, 0.6f));
                card.style.justifyContent = Justify.Center;
                var n = UiFactory.Heading(Charms.Name(charm), 18, RonrikuTheme.Gold);
                card.Add(n);
                var d = UiFactory.Paragraph(Charms.Describe(charm), 13, RonrikuTheme.Text);
                d.style.marginTop = 8;
                card.Add(d);
                UiFactory.Pressable(card);
                card.clicked += () =>
                {
                    if (_committed || _picked.Contains(charm)) return;
                    _picked.Add(charm);
                    UiFactory.SetBorder(card, 3, RonrikuTheme.Teal);
                    Juice.Punch(card, 0.08f);
                    Feedback.Success();
                    if (_picked.Count < picks) return;
                    _committed = true;
                    CharmId[] chosen = _picked.ToArray();
                    schedule.Execute(() => done(chosen)).StartingIn(250);
                };
                Add(card);
                Juice.SlideIn(card, 420f + i * 80f, 0.3f + i * 0.06f);
            }

            if (monster != null)
            {
                Add(UiFactory.Spacer());
                var foe = UiFactory.Row();
                foe.style.justifyContent = Justify.Center;
                foe.style.flexShrink = 0;
                var view = new Voxels.VoxelView(monster, 72, 30f, false);
                view.style.width = view.style.height = 110;
                foe.Add(view);
                var info = new VisualElement();
                info.style.marginLeft = 12;
                var foeName = UiFactory.Heading(monster.Name, 16, palette.Accent);
                foeName.style.unityTextAlign = TextAnchor.MiddleLeft;
                info.Add(foeName);
                if (!string.IsNullOrEmpty(stakes))
                {
                    var stakesLabel = UiFactory.Heading(stakes, 12, RonrikuTheme.Text);
                    stakesLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    stakesLabel.style.marginTop = 6;
                    info.Add(stakesLabel);
                }
                foe.Add(info);
                Add(foe);
            }
        }
    }
}
