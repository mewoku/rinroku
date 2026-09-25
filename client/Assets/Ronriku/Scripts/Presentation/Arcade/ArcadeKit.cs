using System;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    /// <summary>What every arcade screen reports when it ends.</summary>
    public sealed class ArcadeResult
    {
        public bool Won;
        public int Stars;
        public int ElapsedMs;
        public int Score;
        public int BestCombo;
        /// <summary>Compact replay of the player's inputs, stored with the level record and sent to the server.</summary>
        public string Proof;
    }

    /// <summary>
    /// Game feel: shakes, punches, floating numbers, flashes and count-ups. Everything is time based,
    /// self-removing, and respects reduced motion (movement off, feedback text stays).
    /// </summary>
    public static class Juice
    {
        private static float Now => Time.realtimeSinceStartup;

        private static void Animate(VisualElement host, float seconds, Action<float> step, Action done = null)
        {
            float start = Now;
            IVisualElementScheduledItem item = null;
            item = host.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Now - start) / seconds);
                step(t);
                if (t >= 1f)
                {
                    item.Pause();
                    done?.Invoke();
                }
            }).Every(16);
        }

        public static void Shake(VisualElement target, float strength = 8f, float seconds = 0.3f)
        {
            if (MotionSettings.ReducedMotion || target == null) return;
            strength *= RonrikuTuning.Current.shake;
            if (strength <= 0f) return;
            var rng = new System.Random();
            Animate(target, seconds, t =>
            {
                float s = strength * (1f - t);
                target.style.translate = t >= 1f
                    ? new Translate(0, 0)
                    : new Translate((float)(rng.NextDouble() * 2 - 1) * s, (float)(rng.NextDouble() * 2 - 1) * s);
            });
        }

        public static void Punch(VisualElement target, float amount = 0.18f, float seconds = 0.22f)
        {
            if (target == null) return;
            if (MotionSettings.ReducedMotion) return;
            Animate(target, seconds, t =>
            {
                float s = 1f + amount * Mathf.Sin((1f - t) * Mathf.PI * 0.5f) * (1f - t);
                target.style.scale = new Scale(new Vector3(s, s, 1));
            }, () => target.style.scale = new Scale(Vector3.one));
        }

        /// <summary>Floating text that pops, rises and fades, placed in <paramref name="layer"/> coordinates.</summary>
        public static Label Popup(VisualElement layer, Vector2 at, string text, Color color, int size = 28, float rise = 70f, float seconds = 0.9f)
        {
            size = Mathf.RoundToInt(size * RonrikuTuning.Current.popupScale);
            var label = UiFactory.Heading(text, size, color);
            label.style.position = Position.Absolute;
            label.style.left = at.x - 150;
            label.style.width = 300;
            label.style.top = at.y - size;
            label.style.height = size * 2;
            label.style.unityTextOutlineColor = RonrikuTheme.Background;
            label.style.unityTextOutlineWidth = 1.5f;
            layer.Add(label);
            Animate(label, seconds, t =>
            {
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
                label.style.scale = new Scale(new Vector3(pop, pop, 1));
                if (!MotionSettings.ReducedMotion) label.style.translate = new Translate(0, -rise * EaseOut(t));
                label.style.opacity = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }, label.RemoveFromHierarchy);
            return label;
        }

        public static void Flash(VisualElement target, Color color, float seconds = 0.25f)
        {
            var overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = color;
            target.Add(overlay);
            Animate(overlay, seconds, t => overlay.style.opacity = (1f - t) * color.a, overlay.RemoveFromHierarchy);
        }

        public static void CountUp(Label label, int from, int to, float seconds, Func<int, string> format)
        {
            Animate(label, seconds, t => label.text = format(Mathf.RoundToInt(Mathf.Lerp(from, to, EaseOut(t)))));
        }

        /// <summary>Slides an element in from the side with a small overshoot.</summary>
        public static void SlideIn(VisualElement target, float fromX = 420f, float seconds = 0.28f)
        {
            if (MotionSettings.ReducedMotion) return;
            target.style.translate = new Translate(fromX, 0);
            Animate(target, seconds, t =>
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                float over = Mathf.Sin(t * Mathf.PI) * 0.06f;
                target.style.translate = new Translate(fromX * (1f - e) - fromX * over * 0.2f, 0);
            }, () => target.style.translate = new Translate(0, 0));
        }

        public static void Lunge(VisualElement target, float dx, float seconds = 0.26f)
        {
            if (MotionSettings.ReducedMotion) return;
            Animate(target, seconds, t => target.style.translate = new Translate(dx * Mathf.Sin(t * Mathf.PI), -Mathf.Sin(t * Mathf.PI) * 8f),
                () => target.style.translate = new Translate(0, 0));
        }

        public static void FadeOut(VisualElement target, float seconds, Action done = null) =>
            Animate(target, seconds, t => target.style.opacity = 1f - t, done);

        /// <summary>Big centered banner ("FIGHT!", "VICTORY") that slams in and leaves.</summary>
        public static void Banner(VisualElement layer, string text, Color color, float seconds = 0.9f, Action done = null)
        {
            var banner = new PixelLabel(text, color, 7);
            banner.style.position = Position.Absolute;
            banner.style.left = 0;
            banner.style.right = 0;
            banner.style.top = Length.Percent(38);
            banner.style.height = 70;
            banner.pickingMode = PickingMode.Ignore;
            layer.Add(banner);
            Animate(banner, seconds, t =>
            {
                float s = t < 0.12f ? Mathf.Lerp(2.2f, 1f, t / 0.12f) : 1f;
                banner.style.scale = new Scale(new Vector3(s, s, 1));
                banner.style.opacity = t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
            }, () =>
            {
                banner.RemoveFromHierarchy();
                done?.Invoke();
            });
        }

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }

    /// <summary>Pixel shapes drawn with Painter2D, shared by tokens, scales and boards.</summary>
    public static class Glyphs
    {
        public static readonly Color[] TokenColors =
        {
            RonrikuTheme.Hex("11C5B3"), RonrikuTheme.Hex("FF4FD8"), RonrikuTheme.Hex("E8DA37"),
            RonrikuTheme.Hex("FF8A3D"), RonrikuTheme.Hex("3A9BFF")
        };

        public static void Rect(Painter2D p, Vector2 topLeft, float w, float h, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(topLeft);
            p.LineTo(topLeft + new Vector2(w, 0));
            p.LineTo(topLeft + new Vector2(w, h));
            p.LineTo(topLeft + new Vector2(0, h));
            p.ClosePath();
            p.Fill();
        }

        public static void Poly(Painter2D p, Color color, params Vector2[] points)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
        }

        private static Vector2 Rot(Vector2 v, int quarterTurns)
        {
            for (int i = 0; i < (quarterTurns & 3); i++) v = new Vector2(-v.y, v.x);
            return v;
        }

        /// <summary>Shape: 0 circle(octagon) 1 square 2 triangle 3 diamond 4 arrow. r = half size.</summary>
        public static void Shape(Painter2D p, int shape, Vector2 c, float r, Color color, int rotation = 0)
        {
            switch (shape)
            {
                case Token.Circle:
                    float k = r * 0.42f;
                    Poly(p, color, c + new Vector2(-k, -r), c + new Vector2(k, -r), c + new Vector2(r, -k), c + new Vector2(r, k),
                        c + new Vector2(k, r), c + new Vector2(-k, r), c + new Vector2(-r, k), c + new Vector2(-r, -k));
                    break;
                case Token.Square:
                    Rect(p, c - new Vector2(r * 0.85f, r * 0.85f), r * 1.7f, r * 1.7f, color);
                    break;
                case Token.Triangle:
                    Poly(p, color, c + Rot(new Vector2(0, -r), rotation), c + Rot(new Vector2(r, r * 0.8f), rotation), c + Rot(new Vector2(-r, r * 0.8f), rotation));
                    break;
                case Token.Diamond:
                    Poly(p, color, c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0));
                    break;
                default:
                    // Arrow pointing up at rotation 0.
                    Poly(p, color, c + Rot(new Vector2(0, -r), rotation), c + Rot(new Vector2(r, 0), rotation), c + Rot(new Vector2(r * 0.4f, 0), rotation),
                        c + Rot(new Vector2(r * 0.4f, r), rotation), c + Rot(new Vector2(-r * 0.4f, r), rotation), c + Rot(new Vector2(-r * 0.4f, 0), rotation),
                        c + Rot(new Vector2(-r, 0), rotation));
                    break;
            }
        }

        public static void DrawToken(Painter2D p, int token, Vector2 c, float r)
        {
            int count = Domain.Arcade.Token.Count(token);
            int shape = Domain.Arcade.Token.Shape(token);
            Color color = TokenColors[Domain.Arcade.Token.Color(token) % TokenColors.Length];
            int rotation = Domain.Arcade.Token.Rotation(token);
            if (count == 1)
            {
                Shape(p, shape, c, r, color, rotation);
                return;
            }
            float small = r * (count == 2 ? 0.55f : 0.48f);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = count == 2
                    ? new Vector2((i == 0 ? -1 : 1) * r * 0.5f, 0)
                    : new Vector2((i - 1) * r * 0.62f, i == 1 ? -r * 0.45f : r * 0.35f);
                Shape(p, shape, c + offset, small, color, rotation);
            }
        }
    }

    /// <summary>One sequence token or a "?" slot.</summary>
    public sealed class TokenElement : VisualElement
    {
        private int _token;
        private bool _unknown;
        private Color? _frame;

        public TokenElement(int token, bool unknown = false)
        {
            _token = token;
            _unknown = unknown;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(int token, bool unknown)
        {
            _token = token;
            _unknown = unknown;
            MarkDirtyRepaint();
        }

        public void SetFrame(Color? frame)
        {
            _frame = frame;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float side = Mathf.Min(contentRect.width, contentRect.height);
            var c = new Vector2(contentRect.width, contentRect.height) * 0.5f;
            var tl = c - new Vector2(side, side) * 0.5f;
            Glyphs.Rect(p, tl, side, side, RonrikuTheme.NearBlack);
            if (_frame.HasValue)
            {
                float w = 3f;
                Glyphs.Rect(p, tl, side, w, _frame.Value);
                Glyphs.Rect(p, tl + new Vector2(0, side - w), side, w, _frame.Value);
                Glyphs.Rect(p, tl, w, side, _frame.Value);
                Glyphs.Rect(p, tl + new Vector2(side - w, 0), w, side, _frame.Value);
            }
            if (_unknown) PixelLabel.DrawCentered(p, "?", c, side * 0.08f, RonrikuTheme.Yellow);
            else Glyphs.DrawToken(p, _token, c, side * 0.32f);
        }
    }

    /// <summary>Row of pixel hearts.</summary>
    public sealed class HeartsRow : VisualElement
    {
        private readonly PixelIcon[] _hearts;

        public HeartsRow(int max, float size = 22)
        {
            style.flexDirection = FlexDirection.Row;
            _hearts = new PixelIcon[max];
            for (int i = 0; i < max; i++)
            {
                _hearts[i] = new PixelIcon("heart", RonrikuTheme.Red, size);
                _hearts[i].style.marginLeft = 2;
                Add(_hearts[i]);
            }
        }

        public void Set(int hearts)
        {
            for (int i = 0; i < _hearts.Length; i++)
            {
                bool full = i < hearts;
                _hearts[i].SetColor(full ? RonrikuTheme.Red : RonrikuTheme.Line);
            }
        }

        public void Break(int index)
        {
            if (index < 0 || index >= _hearts.Length) return;
            Juice.Punch(_hearts[index], 0.6f, 0.35f);
            Juice.Shake(_hearts[index], 6f, 0.35f);
        }
    }

    /// <summary>Chunky HP bar with a trailing "damage ghost" and the number on top.</summary>
    public sealed class HpBar : VisualElement
    {
        private readonly VisualElement _ghost;
        private readonly VisualElement _fill;
        private readonly Label _text;
        private readonly string _prefix;
        private int _max;
        private float _shown;
        private float _target;

        public HpBar(int max, Color color, string prefix = "HP")
        {
            _max = Mathf.Max(1, max);
            _prefix = prefix;
            _shown = _target = 1f;
            style.height = 22;
            style.backgroundColor = RonrikuTheme.NearBlack;
            UiFactory.SetBorder(this, 2, RonrikuTheme.Line);
            _ghost = new VisualElement();
            _ghost.style.position = Position.Absolute;
            _ghost.style.left = _ghost.style.top = _ghost.style.bottom = 0;
            _ghost.style.backgroundColor = RonrikuTheme.Yellow;
            Add(_ghost);
            _fill = new VisualElement();
            _fill.style.position = Position.Absolute;
            _fill.style.left = _fill.style.top = _fill.style.bottom = 0;
            _fill.style.backgroundImage = new StyleBackground(PixelTextures.DiagonalGradient(color, Color.Lerp(color, Color.black, 0.35f)));
            _fill.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Add(_fill);
            _text = UiFactory.Heading(string.Empty, 11, RonrikuTheme.Text);
            _text.style.position = Position.Absolute;
            _text.style.left = _text.style.right = _text.style.top = _text.style.bottom = 0;
            _text.style.unityTextOutlineColor = RonrikuTheme.Background;
            _text.style.unityTextOutlineWidth = 1f;
            Add(_text);
            Set(max, true);
            schedule.Execute(() =>
            {
                _shown = Mathf.MoveTowards(_shown, _target, 0.012f);
                _ghost.style.width = Length.Percent(_shown * 100f);
            }).Every(16);
        }

        public void Set(int value, bool instant = false)
        {
            _target = Mathf.Clamp01(value / (float)_max);
            _fill.style.width = Length.Percent(_target * 100f);
            if (instant) _shown = _target;
            _text.text = $"{_prefix} {Mathf.Max(0, value)}";
        }
    }

    /// <summary>Balatro-style score readout: [chips] × [mult] with punchy count-ups.</summary>
    public sealed class ChipsMult : VisualElement
    {
        private readonly Label _chips;
        private readonly Label _mult;
        private readonly VisualElement _chipsBox;
        private readonly VisualElement _multBox;

        public static readonly Color ChipsColor = RonrikuTheme.Hex("2F8BFF");
        public static readonly Color MultColor = RonrikuTheme.Hex("FF3B5C");

        public ChipsMult()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            _chipsBox = Box(ChipsColor, out _chips);
            Add(_chipsBox);
            var x = UiFactory.Heading("X", 16, RonrikuTheme.Text);
            x.style.marginLeft = x.style.marginRight = 8;
            Add(x);
            _multBox = Box(MultColor, out _mult);
            Add(_multBox);
            Set(0, 0);
        }

        private static VisualElement Box(Color color, out Label label)
        {
            var box = new VisualElement();
            box.style.minWidth = 84;
            box.style.height = 40;
            box.style.backgroundImage = new StyleBackground(PixelTextures.DiagonalGradient(color, Color.Lerp(color, Color.black, 0.4f)));
            box.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            UiFactory.SetBorder(box, 2, Color.Lerp(color, Color.white, 0.4f));
            box.style.justifyContent = Justify.Center;
            label = UiFactory.Heading("0", 20, RonrikuTheme.Text);
            label.style.unityTextOutlineColor = RonrikuTheme.Background;
            label.style.unityTextOutlineWidth = 1f;
            box.Add(label);
            return box;
        }

        public void Set(int chips, int mult)
        {
            _chips.text = chips.ToString();
            _mult.text = mult.ToString();
        }

        public void PunchChips(int chips)
        {
            _chips.text = chips.ToString();
            Juice.Punch(_chipsBox, 0.25f);
        }

        public void PunchMult(int mult)
        {
            _mult.text = mult.ToString();
            Juice.Punch(_multBox, 0.3f);
        }
    }

    /// <summary>Hero (player's figure) vs monster on a glowing floor, with attack/hurt animations.</summary>
    public sealed class Arena : VisualElement
    {
        private readonly VisualElement _heroSlot;
        private readonly VisualElement _monsterSlot;

        public VisualElement Monster => _monsterSlot;
        public VisualElement Hero => _heroSlot;

        public Arena(Figure hero, Figure monster, Palette palette, float height = 170f)
        {
            style.height = height;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.FlexEnd;
            style.justifyContent = Justify.SpaceBetween;
            style.paddingLeft = style.paddingRight = 10;

            var floor = new VisualElement { pickingMode = PickingMode.Ignore };
            floor.style.position = Position.Absolute;
            floor.style.left = floor.style.right = 0;
            floor.style.bottom = 0;
            floor.style.height = 36;
            floor.style.backgroundImage = new StyleBackground(PixelTextures.VerticalGradient(RonrikuTheme.WithAlpha(palette.Accent, 0f), RonrikuTheme.WithAlpha(palette.Accent, 0.28f)));
            floor.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Add(floor);

            _heroSlot = Slot(hero, height * 0.68f, 40f);
            _monsterSlot = Slot(monster, height * 0.92f, -30f);
            Add(_heroSlot);
            Add(_monsterSlot);
        }

        private static VisualElement Slot(Figure figure, float size, float spin)
        {
            var slot = new VisualElement();
            slot.style.width = slot.style.height = size;
            if (figure != null)
            {
                var view = new VoxelView(figure, 96, spin, false);
                view.style.flexGrow = 1;
                slot.Add(view);
            }
            return slot;
        }

        public void HeroAttack()
        {
            Juice.Lunge(_heroSlot, 90f);
            _monsterSlot.schedule.Execute(() =>
            {
                Juice.Shake(_monsterSlot, 10f, 0.3f);
                Juice.Punch(_monsterSlot, -0.12f, 0.2f);
            }).StartingIn(120);
        }

        public void MonsterAttack()
        {
            Juice.Lunge(_monsterSlot, -90f);
            _heroSlot.schedule.Execute(() =>
            {
                Juice.Shake(_heroSlot, 10f, 0.3f);
                Juice.Punch(_heroSlot, -0.12f, 0.2f);
            }).StartingIn(120);
        }

        public void MonsterDies(Palette palette)
        {
            var burst = new PixelBurstAnchor(palette);
            _monsterSlot.Add(burst);
            Juice.FadeOut(_monsterSlot, 0.6f);
        }

        /// <summary>Centre of the monster in <paramref name="layer"/> coordinates, for damage popups.</summary>
        public Vector2 MonsterCenter(VisualElement layer) => layer.WorldToLocal(_monsterSlot.worldBound.center);
        public Vector2 HeroCenter(VisualElement layer) => layer.WorldToLocal(_heroSlot.worldBound.center);
    }

    internal sealed class PixelBurstAnchor : VisualElement
    {
        public PixelBurstAnchor(Palette palette)
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            Add(new Screens.PixelBurst(palette.Accent, palette.Accent2) { style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 } });
        }
    }

    /// <summary>Swipe / arrow-key input for grid games. Emits a direction 0 up, 1 right, 2 down, 3 left.</summary>
    public sealed class SwipeInput
    {
        private Vector2 _down;
        private int _pointer = -1;
        public event Action<int> Swiped;

        public SwipeInput(VisualElement area)
        {
            area.focusable = true;
            area.RegisterCallback<PointerDownEvent>(e =>
            {
                _pointer = e.pointerId;
                _down = e.position;
                area.Focus();
            });
            area.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _pointer) return;
                _pointer = -1;
                Vector2 d = (Vector2)e.position - _down;
                if (d.magnitude < 18f) return;
                Swiped?.Invoke(Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? 1 : 3) : (d.y > 0 ? 2 : 0));
            });
            area.RegisterCallback<KeyDownEvent>(e =>
            {
                int dir = e.keyCode switch
                {
                    KeyCode.UpArrow or KeyCode.W => 0,
                    KeyCode.RightArrow or KeyCode.D => 1,
                    KeyCode.DownArrow or KeyCode.S => 2,
                    KeyCode.LeftArrow or KeyCode.A => 3,
                    _ => -1
                };
                if (dir >= 0) Swiped?.Invoke(dir);
            });
        }

        public void Emit(int dir) => Swiped?.Invoke(dir);
    }

    /// <summary>A d-pad under grid games for players who prefer buttons over swipes.</summary>
    public static class DPad
    {
        public static VisualElement Build(Action<int> pressed, Color accent)
        {
            var pad = UiFactory.Row();
            pad.style.justifyContent = Justify.Center;
            pad.style.flexShrink = 0;
            string[] labels = { "UP", "RIGHT", "DOWN", "LEFT" };
            foreach (int dir in new[] { 3, 0, 2, 1 })
            {
                int d = dir;
                var b = UiFactory.FlatButton(string.Empty, () => pressed(d));
                b.name = "dpad-" + labels[dir].ToLowerInvariant();
                b.style.width = 72;
                b.style.height = 56;
                b.style.marginLeft = b.style.marginRight = 4;
                b.style.alignItems = Align.Center;
                b.style.justifyContent = Justify.Center;
                var arrow = new ArrowGlyph(d, accent);
                arrow.style.width = arrow.style.height = 26;
                b.Add(arrow);
                pad.Add(b);
            }
            return pad;
        }
    }

    public sealed class ArrowGlyph : VisualElement
    {
        private readonly int _dir;
        private readonly Color _color;

        public ArrowGlyph(int dir, Color color)
        {
            _dir = dir;
            _color = color;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                float r = Mathf.Min(contentRect.width, contentRect.height) * 0.45f;
                Glyphs.Shape(ctx.painter2D, Token.Arrow, contentRect.center, r, _color, _dir);
            };
        }
    }
}
