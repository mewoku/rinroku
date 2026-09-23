using System.Collections.Generic;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Shadow Match host: turn and tip the structure until its shadow covers exactly the teal tiles.
    /// Solving is detected after each settled move and confirmed by replaying the move path.
    /// </summary>
    public sealed class SpatialPuzzleScreen : TrialScreenBase
    {
        private const float RotationSeconds = 0.16f;
        private const float SwipeThreshold = 60f;

        private readonly SpatialPuzzleData _data;
        private readonly SpatialPuzzleValidator _validator = new SpatialPuzzleValidator();
        private readonly IsometricBoardElement _board;
        private readonly ShadowGridElement _currentGrid;
        private readonly List<SpatialMove> _path = new List<SpatialMove>();
        private IVisualElementScheduledItem _animation;
        private int _orientation;
        private int _moves;
        private int _resets;
        private float _animationStartedAt;
        private Quaternion _animationFrom;
        private Quaternion _animationTo;
        private bool _inputLocked;
        private Vector2 _pointerDown;
        private int _activePointer = -1;

        public SpatialPuzzleScreen(SpatialPuzzleData data, TrialScreenContext context)
            : base(context, "SHADOW", "TURN AND TIP UNTIL THE SHADOW FILLS THE TEAL TILES",
                data.Metadata.TimeLimitSeconds, data.Metadata.ContentHash)
        {
            _data = data;
            _orientation = data.StartOrientation;

            var maps = new VisualElement();
            maps.style.flexDirection = FlexDirection.Row;
            maps.style.height = 200;
            maps.style.marginTop = 12;
            maps.Add(MapColumn("TARGET", new ShadowGridElement(data.TargetShadow, RonrikuTheme.Teal)));
            _currentGrid = new ShadowGridElement(data.ShadowAt(_orientation), RonrikuTheme.OffWhite);
            maps.Add(MapColumn("SHADOW", _currentGrid));
            Body.Add(maps);

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
            Body.Add(playfield);

            Controls.Add(ControlRow(
                ("<  TURN", "turn-left-button", () => Move(SpatialMove.TurnLeft)),
                ("TURN  >", "turn-right-button", () => Move(SpatialMove.TurnRight))));
            Controls.Add(ControlRow(
                ("TIP BACK", "tip-back-button", () => Move(SpatialMove.TipBack)),
                ("TIP FORWARD", "tip-forward-button", () => Move(SpatialMove.TipForward))));
            Controls.Add(ControlRow(("UNDO", "undo-button", Undo), ("RESET", "reset-button", Reset)));

            SetStatus("SWIPE THE BOARD OR USE THE CONTROLS", RonrikuTheme.Muted);
            UpdateCounter();
        }

        protected override string CounterPrefix() => $"MOVES {_moves}   //   PAR {_data.Par}";

        protected override TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds) =>
            TrialOutcome.FromSpatial(_data,
                new SpatialPuzzleScorer().Calculate(_data, solved, elapsedMilliseconds, _moves, _resets));

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

        private void Move(SpatialMove move)
        {
            if (_inputLocked || Solved) return;
            int from = _orientation;
            _orientation = CubeOrientations.Apply(_orientation, move);
            _path.Add(move);
            _moves++;
            Haptics.Selection();
            AnimateTo(from, _orientation);
        }

        private void Undo()
        {
            if (_inputLocked || Solved || _path.Count == 0) return;
            SpatialMove last = _path[_path.Count - 1];
            _path.RemoveAt(_path.Count - 1);
            int from = _orientation;
            _orientation = CubeOrientations.Apply(_orientation, CubeOrientations.Inverse(last));
            Haptics.Selection();
            AnimateTo(from, _orientation);
        }

        private void Reset()
        {
            if (_inputLocked || Solved || _path.Count == 0) return;
            int from = _orientation;
            _orientation = _data.StartOrientation;
            _path.Clear();
            _resets++;
            Haptics.Selection();
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
                SetStatus(_path.Count == 0 ? "STARTING POSITION" : "NOT YET", RonrikuTheme.Muted);
                return;
            }
            if (!_validator.IsCorrect(_data, _path))
            {
                SetStatus("MOVE PATH REJECTED   //   RESET", RonrikuTheme.Yellow);
                Haptics.Error();
                return;
            }
            MarkFinished(true, _moves <= _data.Par ? "AT PAR" : "SOLVED", RonrikuTheme.Teal);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_inputLocked || Solved || _activePointer >= 0) return;
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
