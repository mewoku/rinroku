using System;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class TrialScreenContext
    {
        public IHapticsService Haptics;
        public Action Back;
        public Action<TrialOutcome> Completed;
        public string Header;
        public string Footer;
        public string BackLabel = "<";
        public Palette Palette = RonrikuTheme.Lab;
        /// <summary>Optional guardian shown top-right with an HP bar; solving the trial deals the final hit.</summary>
        public Figure Monster;
        /// <summary>HP segments for boss fights (stages); 1 for normal levels.</summary>
        public int HpSegments = 1;
        public int HpRemaining = 1;
    }

    /// <summary>
    /// Shared layout and lifecycle for trial screens: top bar (back, header, guardian + HP), title, body,
    /// counter, status, controls and a CONTINUE button that becomes SKIP once the time limit passes.
    /// </summary>
    public abstract class TrialScreenBase : VisualElement
    {
        private readonly Action<TrialOutcome> _completed;
        private readonly int _timeLimitMs;
        private readonly float _startedAt;
        private readonly IVisualElementScheduledItem _clock;
        private readonly VisualElement _hpBar;
        private readonly VoxelView _monster;
        private readonly TrialScreenContext _context;
        private bool _skipOffered;
        private int _finalElapsed = -1;

        protected readonly IHapticsService Haptics;
        protected readonly Palette Palette;
        protected readonly VisualElement Body;
        protected readonly Label Counter;
        protected readonly Label Status;
        protected readonly VisualElement Controls;
        protected readonly Button Continue;

        protected bool Solved { get; private set; }

        protected TrialScreenBase(TrialScreenContext context, string title, string instruction, int timeLimitSeconds,
            string contentHash)
        {
            _context = context;
            Haptics = context.Haptics;
            Palette = context.Palette;
            _completed = context.Completed;
            _timeLimitMs = timeLimitSeconds * 1000;
            _startedAt = Time.realtimeSinceStartup;

            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.paddingTop = 8;
            style.paddingBottom = 12;

            var top = UiFactory.Row();
            top.style.height = 52;
            top.style.flexShrink = 0;
            var backButton = UiFactory.FlatButton(string.Empty, context.Back);
            backButton.name = "back-button";
            backButton.style.width = 44;
            backButton.style.height = 40;
            backButton.style.paddingLeft = backButton.style.paddingRight = 0;
            backButton.style.alignItems = Align.Center;
            backButton.style.justifyContent = Justify.Center;
            backButton.Add(new PixelIcon("back", RonrikuTheme.Text, 16));
            top.Add(backButton);
            var header = UiFactory.Heading(context.Header, 10, RonrikuTheme.Muted);
            header.style.flexGrow = 1;
            header.style.marginLeft = 10;
            header.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.style.whiteSpace = WhiteSpace.Normal;
            top.Add(header);

            if (context.Monster != null)
            {
                var guardian = new VisualElement();
                guardian.style.alignItems = Align.Center;
                _monster = new VoxelView(context.Monster, 48, 50f, false) { name = "guardian" };
                _monster.style.width = _monster.style.height = 48;
                guardian.Add(_monster);
                _hpBar = UiFactory.Row();
                _hpBar.name = "hp";
                _hpBar.style.height = 6;
                for (int i = 0; i < Mathf.Max(1, context.HpSegments); i++)
                {
                    var seg = new VisualElement();
                    seg.style.width = Mathf.Max(8, 44f / Mathf.Max(1, context.HpSegments) - 2);
                    seg.style.height = 6;
                    seg.style.marginLeft = 1;
                    seg.style.marginRight = 1;
                    seg.style.backgroundColor = i < context.HpRemaining ? RonrikuTheme.Red : RonrikuTheme.Line;
                    _hpBar.Add(seg);
                }
                guardian.Add(_hpBar);
                top.Add(guardian);
            }
            Add(top);

            var titleLabel = new PixelLabel(title, Palette.Accent, 5);
            titleLabel.style.height = 44;
            titleLabel.style.flexShrink = 0;
            Add(titleLabel);
            var instructionLabel = UiFactory.Heading(instruction, 10, RonrikuTheme.Text);
            instructionLabel.style.whiteSpace = WhiteSpace.Normal;
            instructionLabel.style.minHeight = 24;
            instructionLabel.style.flexShrink = 0;
            Add(instructionLabel);

            Body = new VisualElement { name = "trial-body" };
            Body.style.flexGrow = 1;
            Body.style.flexShrink = 1;
            Add(Body);

            Counter = UiFactory.Heading(string.Empty, 12, RonrikuTheme.Text);
            Counter.style.height = 24;
            Counter.style.flexShrink = 0;
            Add(Counter);
            Status = UiFactory.Label(string.Empty, 12, RonrikuTheme.Muted);
            Status.style.height = 22;
            Status.style.flexShrink = 0;
            Add(Status);

            Controls = new VisualElement();
            Controls.style.flexShrink = 0;
            Add(Controls);

            Continue = UiFactory.GlowButton("CONTINUE", Finish, Palette);
            Continue.name = "continue-button";
            Continue.style.height = 48;
            Continue.style.marginTop = 8;
            Continue.style.flexShrink = 0;
            // Reserve the button's space from the start so revealing it never shifts the board.
            Continue.style.visibility = Visibility.Hidden;
            Add(Continue);

            var footer = UiFactory.Label($"{context.Footer}  ·  {contentHash.Substring(0, Math.Min(8, contentHash.Length)).ToUpperInvariant()}", 9, RonrikuTheme.Muted);
            footer.style.height = 18;
            footer.style.flexShrink = 0;
            Add(footer);

            _clock = schedule.Execute(Tick).Every(250);
        }

        protected int ElapsedMilliseconds => _finalElapsed >= 0
            ? _finalElapsed
            : Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f);

        protected abstract string CounterPrefix();

        protected abstract TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds);

        protected void UpdateCounter()
        {
            int ms = ElapsedMilliseconds;
            Counter.text = $"{CounterPrefix()}   {ms / 60000:00}:{ms / 1000 % 60:00}";
        }

        protected void SetStatus(string text, Color color)
        {
            Status.text = text;
            Status.style.color = color;
        }

        /// <summary>Locks input, freezes the timer, hits the guardian and reveals CONTINUE.</summary>
        protected void MarkFinished(bool solved, string statusText, Color statusColor)
        {
            if (_finalElapsed >= 0) return;
            _finalElapsed = ElapsedMilliseconds;
            Solved = solved;
            _clock.Pause();
            UpdateCounter();
            SetStatus(statusText, statusColor);
            Controls.SetEnabled(false);
            Controls.style.opacity = 0.35f;
            Continue.text = "CONTINUE";
            Continue.style.visibility = Visibility.Visible;
            if (solved)
            {
                Feedback.Success();
                HitGuardian();
                Burst(Palette.Accent);
            }
            else Feedback.Error();
        }

        private void HitGuardian()
        {
            if (_monster == null) return;
            Feedback.Hit();
            int remaining = Mathf.Max(0, _context.HpRemaining - 1);
            for (int i = 0; i < _hpBar.childCount; i++)
                _hpBar[i].style.backgroundColor = i < remaining ? RonrikuTheme.Red : RonrikuTheme.Line;
            float start = Time.realtimeSinceStartup;
            IVisualElementScheduledItem shake = null;
            shake = _monster.schedule.Execute(() =>
            {
                float t = Time.realtimeSinceStartup - start;
                _monster.style.translate = new Translate(Mathf.Sin(t * 70f) * 6f * Mathf.Clamp01(1f - t / 0.35f), 0);
                _monster.style.opacity = remaining == 0 ? Mathf.Clamp01(1f - (t - 0.2f) / 0.4f) : 1f;
                if (t > 0.6f) shake.Pause();
            }).Every(16);
        }

        /// <summary>Pixel confetti from the centre of the body.</summary>
        protected void Burst(Color colour)
        {
            if (MotionSettings.ReducedMotion) return;
            var layer = new PixelBurst(colour, Palette.Accent2);
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = 0;
            Add(layer);
        }

        private void Tick()
        {
            if (_finalElapsed >= 0) return;
            UpdateCounter();
            if (_skipOffered || ElapsedMilliseconds < _timeLimitMs) return;
            _skipOffered = true;
            Continue.text = "SKIP";
            Continue.style.visibility = Visibility.Visible;
        }

        private void Finish()
        {
            _clock.Pause();
            _completed?.Invoke(CreateOutcome(Solved, ElapsedMilliseconds));
        }

        protected static VisualElement ControlRow(params (string text, string name, Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8;
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = UiFactory.FlatButton(buttons[i].text, buttons[i].action);
                button.name = buttons[i].name;
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.height = 48;
                if (i > 0) button.style.marginLeft = 8;
                row.Add(button);
            }
            return row;
        }
    }

    /// <summary>Short-lived burst of square pixels with gravity. Removes itself after ~0.9 s.</summary>
    public sealed class PixelBurst : VisualElement
    {
        private readonly Vector2[] _pos = new Vector2[36];
        private readonly Vector2[] _vel = new Vector2[36];
        private readonly Color[] _col = new Color[36];
        private readonly float _start;

        public PixelBurst(Color a, Color b)
        {
            pickingMode = PickingMode.Ignore;
            var rng = new System.Random();
            for (int i = 0; i < _pos.Length; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float speed = 160f + (float)rng.NextDouble() * 260f;
                _vel[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - 0.6f) * speed;
                _col[i] = i % 3 == 0 ? RonrikuTheme.Yellow : i % 2 == 0 ? a : b;
            }
            _start = Time.realtimeSinceStartup;
            generateVisualContent += Draw;
            schedule.Execute(() =>
            {
                MarkDirtyRepaint();
                if (Time.realtimeSinceStartup - _start > 0.9f) RemoveFromHierarchy();
            }).Every(16);
        }

        private void Draw(MeshGenerationContext context)
        {
            float t = Time.realtimeSinceStartup - _start;
            var origin = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.45f);
            var p = context.painter2D;
            float fade = Mathf.Clamp01(1f - t / 0.9f);
            for (int i = 0; i < _pos.Length; i++)
            {
                Vector2 c = origin + _vel[i] * t + new Vector2(0, 520f * t * t);
                float s = 4f + (i % 3) * 2f;
                p.fillColor = new Color(_col[i].r, _col[i].g, _col[i].b, fade);
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(s, 0));
                p.LineTo(c + new Vector2(s, s));
                p.LineTo(c + new Vector2(0, s));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
