using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Logic trial ("Link"): drag from 1 through every cell, passing the numbers in order.
    /// Dragging back over the previous cell retracts; tapping a cell on the path cuts it there.
    /// Numbers out of order and walls cannot be crossed.
    /// </summary>
    public sealed class LogicPuzzleScreen : TrialScreenBase
    {
        public const int OverParPenalty = 15;

        private readonly LogicPuzzleData _data;
        private readonly LogicPuzzleValidator _validator = new LogicPuzzleValidator();
        private readonly LinkBoardElement _board;
        private readonly List<int> _path = new List<int>();
        private int _moves;
        private int _resets;
        private int _activePointer = -1;

        public LogicPuzzleScreen(LogicPuzzleData data, TrialScreenContext context)
            : base(context, "LINK", "CONNECT THE NUMBERS IN ORDER. FILL EVERY CELL", data.Metadata.TimeLimitSeconds,
                data.Metadata.ContentHash)
        {
            _data = data;
            _board = new LinkBoardElement(data) { name = "link-board" };
            _board.style.flexGrow = 1;
            _board.style.marginTop = 16;
            _board.style.marginBottom = 16;
            _board.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _board.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _board.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _board.RegisterCallback<PointerCancelEvent>(_ => _activePointer = -1);
            Body.Add(_board);

            Controls.Add(ControlRow(("UNDO", "undo-button", Undo), ("RESET", "reset-button", Reset)));

            _path.Add(data.StartCell);
            Refresh();
            SetStatus("DRAG FROM 1", RonrikuTheme.Muted);
        }

        public int Par => _data.CellCount - 1;

        protected override string CounterPrefix() => $"CELLS {_path.Count} / {_data.CellCount}";

        protected override TrialOutcome CreateOutcome(bool solved, int elapsedMilliseconds)
        {
            int target = TrialScoring.TargetMilliseconds("logic", _data.Metadata.Difficulty);
            int points = TrialScoring.Points(_data.Metadata.Difficulty, solved, elapsedMilliseconds, target, _moves, Par,
                _resets, OverParPenalty);
            return new TrialOutcome(TrialKind.Logic, _data.Metadata.Difficulty, solved, elapsedMilliseconds, target,
                _moves, Par, _resets, points);
        }

        /// <summary>Extends the path by one cell if the step is legal. Public for automated tests.</summary>
        public bool TryExtend(int cell)
        {
            if (Solved || cell < 0 || _path.Contains(cell)) return false;
            int last = _path[_path.Count - 1];
            if (!_data.CanStep(last, cell)) return false;
            int number = _data.Checkpoints[cell];
            if (number != 0 && number != NextNumber()) return false;
            if (number == _data.CheckpointCount && _path.Count + 1 != _data.CellCount) return false;
            _path.Add(cell);
            _moves++;
            Haptics.Selection();
            Refresh();
            return true;
        }

        private int NextNumber()
        {
            int next = 1;
            foreach (int cell in _path) if (_data.Checkpoints[cell] == next) next++;
            return next;
        }

        private void Retract()
        {
            if (_path.Count <= 1) return;
            _path.RemoveAt(_path.Count - 1);
            Refresh();
        }

        private void TruncateTo(int cell)
        {
            int index = _path.IndexOf(cell);
            if (index < 0 || index == _path.Count - 1) return;
            _path.RemoveRange(index + 1, _path.Count - index - 1);
            Haptics.Selection();
            Refresh();
        }

        private void Undo()
        {
            if (Solved) return;
            Retract();
            Haptics.Selection();
        }

        private void Reset()
        {
            if (Solved || _path.Count <= 1) return;
            _path.RemoveRange(1, _path.Count - 1);
            _resets++;
            Haptics.Selection();
            Refresh();
        }

        private void Refresh()
        {
            bool complete = _path.Count == _data.CellCount && _validator.IsCorrect(_data, _path);
            _board.SetPath(_path, complete);
            UpdateCounter();
            if (complete)
            {
                MarkFinished(true, _moves <= Par ? "PERFECT ROUTE" : "LINKED", RonrikuTheme.Teal);
                return;
            }
            if (_path.Count > 1)
                SetStatus($"NEXT {NextNumber()}", RonrikuTheme.Muted);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (Solved || _activePointer >= 0) return;
            int cell = _board.CellAt(evt.localPosition);
            if (cell < 0) return;
            _activePointer = evt.pointerId;
            _board.CapturePointer(evt.pointerId);
            if (cell != _path[_path.Count - 1]) TruncateTo(cell);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _activePointer || Solved) return;
            int cell = _board.CellAt(evt.localPosition);
            if (cell < 0 || cell == _path[_path.Count - 1]) return;
            if (_path.Count > 1 && cell == _path[_path.Count - 2]) Retract();
            else TryExtend(cell);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointer) return;
            _board.ReleasePointer(evt.pointerId);
            _activePointer = -1;
        }
    }
}
