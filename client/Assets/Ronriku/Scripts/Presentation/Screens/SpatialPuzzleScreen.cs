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
        private const float SwipeThreshold = 24f;

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
        private readonly PixelIcon _hint;

        public SpatialPuzzleScreen(SpatialPuzzleData data, TrialScreenContext context)
            : base(context, "SHADOW", "MAKE THE SHADOW FILL THE GLOWING TILES",
                data.Metadata.TimeLimitSeconds, data.Metadata.ContentHash)
        {
            _data = data;
            _orientation = data.StartOrientation;

            var maps = new VisualElement();
            maps.style.flexDirection = FlexDirection.Row;
            maps.style.height = 92;
            maps.style.marginTop = 4;
            maps.style.flexShrink = 0;
            maps.Add(MapColumn("TARGET", new ShadowGridElement(data.TargetShadow, RonrikuTheme.Teal)));
            _currentGrid = new ShadowGridElement(data.ShadowAt(_orientation), RonrikuTheme.OffWhite);
            maps.Add(MapColumn("SHADOW", _currentGrid));
            Body.Add(maps);

            _board = new IsometricBoardElement(data.Cubes, _orientation, data.TargetShadow);
            _board.style.position = Position.Absolute;
            _board.style.left = _board.style.right = _board.style.top = _board.style.bottom = 0;
            var playfield = new VisualElement { name = "spatial-playfield" };
            playfield.style.flexGrow = 1;
            playfield.style.minHeight = 200;
            playfield.Add(_board);
            _hint = new PixelIcon("hand", RonrikuTheme.Text, 36) { name = "swipe-hint" };
            _hint.style.position = Position.Absolute;
            _hint.style.left = Length.Percent(50);
            _hint.style.top = Length.Percent(55);
            playfield.Add(_hint);
            _hint.schedule.Execute(() =>
            {
                if (_path.Count > 0 || Solved)
                {
                    _hint.style.display = DisplayStyle.None;
                    return;
                }
                float t = Mathf.Repeat(Time.realtimeSinceStartup * 0.7f, 1f);
                _hint.style.translate = new Translate(Mathf.Lerp(50f, -70f, Mathf.SmoothStep(0, 1, t)), 0);
                _hint.style.opacity = Mathf.Sin(t * Mathf.PI) * 0.8f;
            }).Every(33);
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

            SetStatus("SWIPE TO TURN  ·  SWIPE UP/DOWN TO TIP", RonrikuTheme.Muted);
            UpdateCounter();
        }

        protected override string CounterPrefix() => $"MOVES {_moves}   //   PAR {_data.Par}";

        protected override TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds) =>
            TrialOutcome.FromSpatial(_data,
                new SpatialPuzzleScorer().Calculate(_data, solved, elapsedMilliseconds, _moves, _resets),
                "[" + string.Join(",", _path.ConvertAll(m => ((int)m).ToString())) + "]");

        private static VisualElement MapColumn(string label, VisualElement grid)
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            column.style.alignItems = Align.Center;
            var caption = UiFactory.Heading(label, 9, label == "TARGET" ? RonrikuTheme.Teal : RonrikuTheme.Muted);
            caption.style.height = 18;
            column.Add(caption);
            grid.style.width = 110;
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
            Accessibility.Feedback.Move();
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
                int overlap = OverlapCount();
                SetStatus(_path.Count == 0 ? "STARTING POSITION" : $"{overlap} / {TargetCount()} TILES COVERED", RonrikuTheme.Muted);
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

        private int TargetCount()
        {
            int n = 0;
            for (int i = 0; i < 9; i++) if ((_data.TargetShadow & (1 << i)) != 0) n++;
            return n;
        }

        private int OverlapCount()
        {
            int both = _data.TargetShadow & _data.ShadowAt(_orientation), n = 0;
            for (int i = 0; i < 9; i++) if ((both & (1 << i)) != 0) n++;
            return n;
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
