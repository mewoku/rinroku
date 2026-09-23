using System;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Shared layout and lifecycle for trial screens: top bar, title, instruction, body, counter,
    /// status, controls, and a CONTINUE button that becomes SKIP TRIAL once the time limit passes.
    /// </summary>
    public abstract class TrialScreenBase : VisualElement
    {
        private readonly Action<TrialOutcome> _completed;
        private readonly int _timeLimitMs;
        private readonly float _startedAt;
        private readonly IVisualElementScheduledItem _clock;
        private bool _skipOffered;
        private int _finalElapsed = -1;

        protected readonly IHapticsService Haptics;
        protected readonly VisualElement Body;
        protected readonly Label Counter;
        protected readonly Label Status;
        protected readonly VisualElement Controls;
        protected readonly Button Continue;

        protected bool Solved { get; private set; }

        protected TrialScreenBase(TrialScreenContext context, string title, string instruction, int timeLimitSeconds,
            string contentHash)
        {
            Haptics = context.Haptics;
            _completed = context.Completed;
            _timeLimitMs = timeLimitSeconds * 1000;
            _startedAt = Time.realtimeSinceStartup;

            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 36;
            style.paddingBottom = 24;

            var top = new VisualElement();
            top.style.height = 80;
            top.style.flexDirection = FlexDirection.Row;
            var backButton = UiFactory.Button(context.BackLabel, context.Back);
            backButton.name = "back-button";
            backButton.style.width = 170;
            backButton.style.height = 56;
            top.Add(backButton);
            var header = UiFactory.Label(context.Header, 16, RonrikuTheme.Muted, FontStyle.Bold);
            header.style.flexGrow = 1;
            header.style.unityTextAlign = TextAnchor.MiddleRight;
            top.Add(header);
            Add(top);

            var titleLabel = new PixelLabel(title, RonrikuTheme.Teal, 9);
            titleLabel.style.height = 84;
            Add(titleLabel);
            var instructionLabel = UiFactory.Label(instruction, 16, RonrikuTheme.OffWhite, FontStyle.Bold);
            instructionLabel.style.height = 44;
            Add(instructionLabel);

            Body = new VisualElement { name = "trial-body" };
            Body.style.flexGrow = 1;
            Add(Body);

            Counter = UiFactory.Label(string.Empty, 20, RonrikuTheme.OffWhite, FontStyle.Bold);
            Counter.style.height = 44;
            Add(Counter);
            Status = UiFactory.Label(string.Empty, 15, RonrikuTheme.Muted, FontStyle.Bold);
            Status.style.height = 48;
            Add(Status);

            Controls = new VisualElement();
            Add(Controls);

            Continue = UiFactory.Button("CONTINUE", Finish, true);
            Continue.name = "continue-button";
            Continue.style.height = 72;
            Continue.style.marginTop = 12;
            // Reserve the button's space from the start so revealing it never shifts the board.
            Continue.style.visibility = Visibility.Hidden;
            Add(Continue);

            var footer = UiFactory.Label($"{context.Footer}   //   {contentHash.ToUpperInvariant()}", 12, RonrikuTheme.Muted);
            footer.style.height = 40;
            Add(footer);

            _clock = schedule.Execute(Tick).Every(250);
        }

        protected int ElapsedMilliseconds => _finalElapsed >= 0
            ? _finalElapsed
            : Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f);

        /// <summary>Text for the counter line, without the timer.</summary>
        protected abstract string CounterPrefix();

        /// <summary>Outcome for the current state; called once when the player continues or skips.</summary>
        protected abstract TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds);

        protected void UpdateCounter()
        {
            int ms = ElapsedMilliseconds;
            Counter.text = $"{CounterPrefix()}   //   {ms / 60000:00}:{ms / 1000 % 60:00}";
        }

        protected void SetStatus(string text, Color color)
        {
            Status.text = text;
            Status.style.color = color;
        }

        /// <summary>Locks input, freezes the timer, and reveals CONTINUE.</summary>
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
            Continue.style.backgroundColor = RonrikuTheme.Teal;
            Continue.style.color = RonrikuTheme.Black;
            Continue.style.visibility = Visibility.Visible;
            if (solved) Haptics.Success(); else Haptics.Error();
        }

        protected static VisualElement ControlRow(params (string text, string name, Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 12;
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = UiFactory.Button(buttons[i].text, buttons[i].action);
                button.name = buttons[i].name;
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.height = 64;
                if (i > 0) button.style.marginLeft = 12;
                row.Add(button);
            }
            return row;
        }

        private void Tick()
        {
            if (_finalElapsed >= 0) return;
            UpdateCounter();
            if (_skipOffered || ElapsedMilliseconds < _timeLimitMs) return;
            _skipOffered = true;
            Continue.text = "SKIP TRIAL";
            Continue.style.backgroundColor = RonrikuTheme.NearBlack;
            Continue.style.color = RonrikuTheme.OffWhite;
            Continue.style.visibility = Visibility.Visible;
        }

        private void Finish()
        {
            _clock.Pause();
            _completed?.Invoke(CreateOutcome(Solved, ElapsedMilliseconds));
        }
    }

    public sealed class TrialScreenContext
    {
        public IHapticsService Haptics;
        public Action Back;
        public Action<TrialOutcome> Completed;
        public string Header;
        public string Footer;
        public string BackLabel = "<  HOME";
    }
}
