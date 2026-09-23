using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class SpatialPuzzleScreen : VisualElement
    {
        private readonly SpatialPuzzleData _data;
        private readonly SpatialPuzzleValidator _validator = new SpatialPuzzleValidator();
        private readonly IHapticsService _haptics;
        private readonly IsometricBoardElement _board;
        private readonly Label _status;
        private readonly Stack<int> _history = new Stack<int>();
        private IVisualElementScheduledItem _animation;
        private int _orientation;
        private int _rotations;
        private int _resets;
        private float _startedAt;
        private float _animationStartedAt;
        private float _animationFrom;
        private float _animationTo;
        private bool _inputLocked;
        private Vector2 _pointerDown;
        private int _activePointer = -1;

        public SpatialPuzzleScreen(SpatialPuzzleData data, IHapticsService haptics, Action back)
        {
            _data = data;
            _haptics = haptics;
            _orientation = data.StartOrientation;
            _startedAt = Time.realtimeSinceStartup;
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 36;

            var top = new VisualElement();
            top.style.height = 80;
            top.style.flexDirection = FlexDirection.Row;
            var backButton = UiFactory.Button("<  DAILY", back);
            backButton.style.width = 170;
            backButton.style.height = 56;
            top.Add(backButton);
            var trial = UiFactory.Label("TRIAL 1 / 3   //   SPATIAL", 16, RonrikuTheme.Muted, FontStyle.Bold);
            trial.style.flexGrow = 1;
            trial.style.unityTextAlign = TextAnchor.MiddleRight;
            top.Add(trial); Add(top);

            var title = new PixelLabel("MATCH", RonrikuTheme.Teal, 9);
            title.style.height = 84;
            Add(title);
            var instruction = UiFactory.Label("ROTATE THE STRUCTURE TO MATCH THE TARGET", 17, RonrikuTheme.OffWhite, FontStyle.Bold);
            instruction.style.height = 48;
            Add(instruction);

            var targetRow = new VisualElement();
            targetRow.style.height = 330;
            targetRow.style.alignItems = Align.Center;
            targetRow.Add(UiFactory.Label("TARGET", 14, RonrikuTheme.Yellow, FontStyle.Bold));
            var target = new IsometricBoardElement(data.Cubes, data.TargetOrientation, true);
            target.style.width = 330; target.style.height = 280;
            targetRow.Add(target); Add(targetRow);

            _board = new IsometricBoardElement(data.Cubes, _orientation);
            _board.style.flexGrow = 1;
            _board.style.minHeight = 500;
            var playfield = new VisualElement { name = "spatial-playfield" };
            playfield.style.flexGrow = 1;
            playfield.style.minHeight = 500;
            playfield.Add(_board);
            _board.style.position = Position.Absolute;
            _board.style.left = _board.style.right = _board.style.top = _board.style.bottom = 0;
            playfield.RegisterCallback<PointerDownEvent>(OnPointerDown);
            playfield.RegisterCallback<PointerUpEvent>(OnPointerUp);
            playfield.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            Add(playfield);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.height = 84;
            var left = UiFactory.Button("ROTATE LEFT", () => Rotate(-1));
            var right = UiFactory.Button("ROTATE RIGHT", () => Rotate(1));
            left.style.flexGrow = right.style.flexGrow = 1;
            left.style.marginRight = 7;
            right.style.marginLeft = 7;
            controls.Add(left); controls.Add(right); Add(controls);

            var utilities = new VisualElement();
            utilities.style.flexDirection = FlexDirection.Row;
            utilities.style.height = 68;
            var undo = UiFactory.Button("UNDO", Undo);
            var reset = UiFactory.Button("RESET", Reset);
            undo.style.flexGrow = reset.style.flexGrow = 1;
            undo.style.height = reset.style.height = 54;
            undo.style.marginRight = 7;
            reset.style.marginLeft = 7;
            utilities.Add(undo); utilities.Add(reset); Add(utilities);

            _status = UiFactory.Label("SWIPE OR TAP ROTATE   //   CHECK WHEN READY", 15, RonrikuTheme.Muted, FontStyle.Bold);
            _status.style.height = 56;
            Add(_status);
            var check = UiFactory.Button("CHECK", Check, true);
            check.name = "check-button";
            check.style.height = 72;
            Add(check);
            var footer = UiFactory.Label($"LOCAL PRACTICE   //   {data.Metadata.ContentHash.ToUpperInvariant()}", 12, RonrikuTheme.Muted);
            footer.style.height = 64;
            Add(footer);
        }

        private void Rotate(int delta)
        {
            if (_inputLocked) return;
            _history.Push(_orientation);
            int from = _orientation;
            _orientation = (_orientation + delta + 4) & 3;
            _rotations++;
            AnimateRotation(from, from + delta, _orientation);
            _status.text = $"ORIENTATION {_orientation + 1} / 4";
            _status.style.color = RonrikuTheme.Muted;
            _haptics.Selection();
        }

        private void Check()
        {
            if (_inputLocked) return;
            bool correct = _validator.IsCorrect(_data, _orientation);
            int elapsed = Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f);
            SpatialAttemptScore score = new SpatialPuzzleScorer().Calculate(_data, correct, elapsed, _rotations, _resets);
            _status.text = correct ? $"MATCH CONFIRMED   //   {score.Points} PTS" : "NOT A MATCH   //   KEEP ROTATING";
            _status.style.color = correct ? RonrikuTheme.Teal : RonrikuTheme.Yellow;
            if (correct) _haptics.Success(); else _haptics.Error();
        }

        private void Undo()
        {
            if (_inputLocked || _history.Count == 0) return;
            int target = _history.Pop();
            int from = _orientation;
            int delta = target - from;
            if (delta > 2) delta -= 4;
            if (delta < -2) delta += 4;
            _orientation = target;
            AnimateRotation(from, from + delta, target);
            _status.text = "LAST ROTATION UNDONE";
            _status.style.color = RonrikuTheme.Muted;
            _haptics.Selection();
        }

        private void Reset()
        {
            if (_inputLocked) return;
            _history.Clear();
            int from = _orientation;
            int target = _data.StartOrientation;
            int delta = target - from;
            if (delta > 2) delta -= 4;
            if (delta < -2) delta += 4;
            _orientation = target;
            _resets++;
            AnimateRotation(from, from + delta, target);
            _status.text = "STARTING ORIENTATION RESTORED";
            _status.style.color = RonrikuTheme.Muted;
            _haptics.Selection();
        }

        private void AnimateRotation(float from, float to, int finalOrientation)
        {
            bool reducedMotion = PlayerPrefs.GetInt("ronriku.reducedMotion", 0) == 1;
            if (reducedMotion)
            {
                _board.SetOrientation(finalOrientation);
                return;
            }
            _animation?.Pause();
            _inputLocked = true;
            _animationFrom = from;
            _animationTo = to;
            _animationStartedAt = Time.realtimeSinceStartup;
            _animation = schedule.Execute(() => TickRotation(finalOrientation)).Every(16);
        }

        private void TickRotation(int finalOrientation)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _animationStartedAt) / 0.14f);
            float eased = t * t * (3f - 2f * t);
            _board.SetDisplayRotation(Mathf.Lerp(_animationFrom, _animationTo, eased));
            if (t < 1f) return;
            _animation?.Pause();
            _board.SetOrientation(finalOrientation);
            _inputLocked = false;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_inputLocked || _activePointer >= 0) return;
            _activePointer = evt.pointerId;
            _pointerDown = evt.position;
            (evt.currentTarget as VisualElement)?.CapturePointer(evt.pointerId);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointer) return;
            (evt.currentTarget as VisualElement)?.ReleasePointer(evt.pointerId);
            float delta = evt.position.x - _pointerDown.x;
            _activePointer = -1;
            if (Mathf.Abs(delta) >= 60f) Rotate(delta < 0f ? 1 : -1);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _activePointer) _activePointer = -1;
        }
    }
}
