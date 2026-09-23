using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Shadow Match host: turn and tip the structure until its shadow covers exactly the teal tiles.
    /// Solving is detected after each settled move and confirmed by replaying the move path.
    /// </summary>
    public sealed class SpatialPuzzleScreen : VisualElement
    {
        private const float RotationSeconds = 0.16f;
        private const float SwipeThreshold = 60f;

        private readonly SpatialPuzzleData _data;
        private readonly SpatialPuzzleValidator _validator = new SpatialPuzzleValidator();
        private readonly IHapticsService _haptics;
        private readonly Action<SpatialAttemptScore> _completed;
        private readonly IsometricBoardElement _board;
        private readonly ShadowGridElement _currentGrid;
        private readonly Label _status;
        private readonly Label _counter;
        private readonly VisualElement _controls;
        private readonly Button _continue;
        private readonly List<SpatialMove> _path = new List<SpatialMove>();
        private IVisualElementScheduledItem _animation;
        private IVisualElementScheduledItem _clock;
        private bool _skipOffered;
        private SpatialAttemptScore _score;
        private int _orientation;
        private int _moves;
        private int _resets;
        private float _startedAt;
        private float _animationStartedAt;
        private Quaternion _animationFrom;
        private Quaternion _animationTo;
        private bool _inputLocked;
        private bool _solved;
        private Vector2 _pointerDown;
        private int _activePointer = -1;

        public SpatialPuzzleScreen(SpatialPuzzleData data, IHapticsService haptics, Action back,
            Action<SpatialAttemptScore> completed, string header = "SPATIAL   //   PRACTICE",
            string footer = null, string backLabel = "<  DAILY")
        {
            _data = data;
            _haptics = haptics;
            _completed = completed;
            _orientation = data.StartOrientation;
            _startedAt = Time.realtimeSinceStartup;
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 36;
            style.paddingBottom = 24;

            var top = new VisualElement();
            top.style.height = 80;
            top.style.flexDirection = FlexDirection.Row;
            var backButton = UiFactory.Button(backLabel, back);
            backButton.name = "back-button";
            backButton.style.width = 170;
            backButton.style.height = 56;
            top.Add(backButton);
            var trial = UiFactory.Label(header, 16, RonrikuTheme.Muted, FontStyle.Bold);
            trial.style.flexGrow = 1;
            trial.style.unityTextAlign = TextAnchor.MiddleRight;
            top.Add(trial);
            Add(top);

            var title = new PixelLabel("SHADOW", RonrikuTheme.Teal, 9);
            title.style.height = 84;
            Add(title);
            var instruction = UiFactory.Label("TURN AND TIP UNTIL THE SHADOW FILLS THE TEAL TILES", 16,
                RonrikuTheme.OffWhite, FontStyle.Bold);
            instruction.style.height = 44;
            Add(instruction);

            var maps = new VisualElement();
            maps.style.flexDirection = FlexDirection.Row;
            maps.style.height = 200;
            maps.style.marginTop = 12;
            maps.Add(MapColumn("TARGET", new ShadowGridElement(data.TargetShadow, RonrikuTheme.Teal)));
            _currentGrid = new ShadowGridElement(data.ShadowAt(_orientation), RonrikuTheme.OffWhite);
            maps.Add(MapColumn("SHADOW", _currentGrid));
            Add(maps);

            _board = new IsometricBoardElement(data.Cubes, _orientation, data.TargetShadow);
            _board.style.position = Position.Absolute;
            _board.style.left = _board.style.right = _board.style.top = _board.style.bottom = 0;
            var playfield = new VisualElement { name = "spatial-playfield" };
            playfield.style.flexGrow = 1;
            playfield.style.minHeight = 420;
            playfield.Add(_board);
            playfield.RegisterCallback<PointerDownEvent>(OnPointerDown);
            playfield.RegisterCallback<PointerUpEvent>(OnPointerUp);
            playfield.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            Add(playfield);

            _counter = UiFactory.Label(string.Empty, 20, RonrikuTheme.OffWhite, FontStyle.Bold);
            _counter.style.height = 44;
            Add(_counter);
            _status = UiFactory.Label("SWIPE THE BOARD OR USE THE CONTROLS", 15, RonrikuTheme.Muted, FontStyle.Bold);
            _status.style.height = 48;
            Add(_status);

            _controls = new VisualElement();
            _controls.Add(ControlRow(
                ("<  TURN", "turn-left-button", () => Move(SpatialMove.TurnLeft)),
                ("TURN  >", "turn-right-button", () => Move(SpatialMove.TurnRight))));
            _controls.Add(ControlRow(
                ("TIP BACK", "tip-back-button", () => Move(SpatialMove.TipBack)),
                ("TIP FORWARD", "tip-forward-button", () => Move(SpatialMove.TipForward))));
            var utilities = ControlRow(("UNDO", "undo-button", Undo), ("RESET", "reset-button", Reset));
            foreach (var child in utilities.Children()) child.style.height = 54;
            _controls.Add(utilities);
            Add(_controls);

            _continue = UiFactory.Button("CONTINUE", Finish, true);
            _continue.name = "continue-button";
            _continue.style.height = 72;
            _continue.style.marginTop = 12;
            _continue.style.display = DisplayStyle.None;
            Add(_continue);

            var footerLabel = UiFactory.Label(
                $"{footer ?? "LOCAL PRACTICE"}   //   {data.Metadata.ContentHash.ToUpperInvariant()}", 12,
                RonrikuTheme.Muted);
            footerLabel.style.height = 40;
            Add(footerLabel);

            UpdateCounter();
            _clock = schedule.Execute(Tick).Every(250);
        }

        private static VisualElement MapColumn(string label, VisualElement grid)
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            column.style.alignItems = Align.Center;
            var caption = UiFactory.Label(label, 14, label == "TARGET" ? RonrikuTheme.Teal : RonrikuTheme.Muted,
                FontStyle.Bold);
            caption.style.height = 32;
            column.Add(caption);
            grid.style.width = 220;
            grid.style.flexGrow = 1;
            column.Add(grid);
            return column;
        }

        private static VisualElement ControlRow(params (string text, string name, Action action)[] buttons)
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
                button.style.height = 76;
                if (i > 0) button.style.marginLeft = 12;
                row.Add(button);
            }
            return row;
        }

        private void Move(SpatialMove move)
        {
            if (_inputLocked || _solved) return;
            int from = _orientation;
            _orientation = CubeOrientations.Apply(_orientation, move);
            _path.Add(move);
            _moves++;
            _haptics.Selection();
            AnimateTo(from, _orientation);
        }

        private void Undo()
        {
            if (_inputLocked || _solved || _path.Count == 0) return;
            SpatialMove last = _path[_path.Count - 1];
            _path.RemoveAt(_path.Count - 1);
            int from = _orientation;
            _orientation = CubeOrientations.Apply(_orientation, CubeOrientations.Inverse(last));
            _haptics.Selection();
            AnimateTo(from, _orientation);
        }

        private void Reset()
        {
            if (_inputLocked || _solved || _path.Count == 0) return;
            int from = _orientation;
            _orientation = _data.StartOrientation;
            _path.Clear();
            _resets++;
            _haptics.Selection();
            AnimateTo(from, _orientation);
        }

        private void AnimateTo(int from, int to)
        {
            UpdateCounter();
            bool reducedMotion = PlayerPrefs.GetInt("ronriku.reducedMotion", 0) == 1;
            if (reducedMotion || from == to)
            {
                _board.SetOrientation(to);
                Settle();
                return;
            }
            _animation?.Pause();
            _inputLocked = true;
            _animationFrom = IsometricBoardElement.ToQuaternion(from);
            _animationTo = IsometricBoardElement.ToQuaternion(to);
            _animationStartedAt = Time.realtimeSinceStartup;
            _animation = schedule.Execute(() => TickRotation(to)).Every(16);
        }

        private void TickRotation(int finalOrientation)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _animationStartedAt) / RotationSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            _board.SetDisplayRotation(Quaternion.Slerp(_animationFrom, _animationTo, eased));
            if (t < 1f) return;
            _animation?.Pause();
            _board.SetOrientation(finalOrientation);
            _inputLocked = false;
            Settle();
        }

        private void Settle()
        {
            bool match = _data.IsSolvedAt(_orientation);
            _currentGrid.SetMask(_data.ShadowAt(_orientation), match ? RonrikuTheme.Teal : RonrikuTheme.OffWhite);
            if (!match)
            {
                _status.text = _path.Count == 0 ? "STARTING POSITION" : "NOT YET";
                _status.style.color = RonrikuTheme.Muted;
                return;
            }

            bool confirmed = _validator.IsCorrect(_data, _path);
            int elapsed = ElapsedMilliseconds;
            _score = new SpatialPuzzleScorer().Calculate(_data, confirmed, elapsed, _moves, _resets);
            if (!confirmed)
            {
                _status.text = "MOVE PATH REJECTED   //   RESET";
                _status.style.color = RonrikuTheme.Yellow;
                _haptics.Error();
                return;
            }

            _solved = true;
            _clock?.Pause();
            UpdateCounter();
            _continue.text = "CONTINUE";
            _continue.style.backgroundColor = RonrikuTheme.Teal;
            _continue.style.color = RonrikuTheme.Black;
            _haptics.Success();
            _status.text = _score.AtPar
                ? $"AT PAR   //   {FormatTime(elapsed)}   //   {_score.Points} PTS"
                : $"SOLVED   //   {FormatTime(elapsed)}   //   {_score.Points} PTS";
            _status.style.color = RonrikuTheme.Teal;
            _controls.SetEnabled(false);
            _controls.style.opacity = 0.35f;
            _continue.style.display = DisplayStyle.Flex;
        }

        private int ElapsedMilliseconds => Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f);

        private void UpdateCounter()
        {
            int shown = _solved ? _score.ElapsedMilliseconds : ElapsedMilliseconds;
            _counter.text = $"MOVES {_moves}   //   PAR {_data.Par}   //   {shown / 60000:00}:{shown / 1000 % 60:00}";
        }

        private void Tick()
        {
            if (_solved) return;
            UpdateCounter();
            if (_skipOffered || ElapsedMilliseconds < _data.Metadata.TimeLimitSeconds * 1000) return;
            _skipOffered = true;
            _continue.text = "SKIP TRIAL";
            _continue.style.backgroundColor = RonrikuTheme.NearBlack;
            _continue.style.color = RonrikuTheme.OffWhite;
            _continue.style.display = DisplayStyle.Flex;
        }

        private void Finish()
        {
            _clock?.Pause();
            if (!_solved)
                _score = new SpatialPuzzleScorer().Calculate(_data, false, ElapsedMilliseconds, _moves, _resets);
            _completed?.Invoke(_score);
        }

        private static string FormatTime(int ms) => $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms / 10 % 100:00}";

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_inputLocked || _solved || _activePointer >= 0) return;
            _activePointer = evt.pointerId;
            _pointerDown = evt.position;
            (evt.currentTarget as VisualElement)?.CapturePointer(evt.pointerId);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointer) return;
            (evt.currentTarget as VisualElement)?.ReleasePointer(evt.pointerId);
            _activePointer = -1;
            Vector2 delta = (Vector2)evt.position - _pointerDown;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < SwipeThreshold) return;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                Move(delta.x < 0f ? SpatialMove.TurnLeft : SpatialMove.TurnRight);
            else
                Move(delta.y > 0f ? SpatialMove.TipForward : SpatialMove.TipBack);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _activePointer) _activePointer = -1;
        }
    }
}
