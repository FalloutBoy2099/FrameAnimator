namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Geometry;
    using Model;

    /// <summary>
    /// Editing operations (UI-independent) with undo handling, following the logic of the original's
    /// <c>acFramesOffset</c>, <c>acFrameOffset</c>, <c>acOffsetWizard</c>, mouse and <c>acUndo</c> handlers.
    /// </summary>
    public sealed class ProjectEditor(Project project)
    {
        public Project Project { get; } = project;

        /// <summary>Movement angles (in degrees) per direction, used by the Shift constraint and the wizard (0x40ef6d…).</summary>
        public static readonly Double[] DirectionAngles = [-36.9, 0.0, 36.9, 143.1, 180.0, -143.1];

        public static Double DirectionTangent(Int32 direction) => Math.Tan(DirectionAngles[direction] * (Math.PI / 180.0));

        private Direction Dir(Int32 d) => Project.Directions[d];

        private static Int32 Round(Double v) => (Int32)Math.Round(v, MidpointRounding.ToEven);

        // ------------------------------------------------------------------ frames

        /// <summary>"Frame Down / Up" (0x40c730): moves frame <paramref name="index"/> by <paramref name="delta"/> and clears the undo history.</summary>
        public void MoveFrame(Int32 direction, Int32 index, Int32 delta)
        {
            var frames = Dir(direction).Frames;
            Int32 target = index + delta;
            var f = frames[index];
            frames.RemoveAt(index);
            frames.Insert(target, f);
            Project.Undo.Clear();
        }

        /// <summary>"Remove Frames": removes the given indices (in any order) and clears the undo history.</summary>
        public void RemoveFrames(Int32 direction, IEnumerable<Int32> indices)
        {
            var frames = Dir(direction).Frames;
            foreach (Int32 i in indices.Distinct().OrderByDescending(x => x))
                if (i >= 0 && i < frames.Count)
                    frames.RemoveAt(i);
            Project.Undo.Clear();
        }

        // ------------------------------------------------------------------ offsets (dialog boxes)

        /// <summary>Initial values of "Frames Offset": <c>GetShift</c> after auto-cutting frame 0.</summary>
        public (Int32 X, Int32 Y) GetFramesOffset(Int32 direction)
        {
            var dir = Dir(direction);
            FrameMath.AutoCut(Project, dir.Frames[0]);
            return FrameMath.GetShift(dir);
        }

        /// <summary>Validation of "Frames Offset": records an undo entry if changed, then <c>SetShift</c>.</summary>
        public void SetFramesOffset(Int32 direction, Int32 x, Int32 y)
        {
            var dir = Dir(direction);
            var initial = GetFramesOffset(direction);
            if (initial.X != x || initial.Y != y)
                Project.Undo.Push(new UndoRecord((Int16)direction, -1, dir.ShiftBaseX, dir.ShiftBaseY, false));
            FrameMath.SetShift(dir, x, y);
        }

        /// <summary>
        /// Initial values of "Frame Offset" / "First Frame Offset": relative to the previous frame for
        /// <paramref name="index"/> &gt; 0, <c>FirstFrameOffset</c> otherwise.
        /// </summary>
        public (Int32 X, Int32 Y) GetFrameOffset(Int32 direction, Int32 index)
        {
            var dir = Dir(direction);
            if (index <= 0)
                return (dir.FirstFrameOffsetX, dir.FirstFrameOffsetY);
            var prev = dir.Frames[index - 1];
            var cur = dir.Frames[index];
            FrameMath.AutoCut(Project, prev);
            FrameMath.AutoCut(Project, cur);
            return FrameMath.Relative(prev, cur);
        }

        /// <summary>Validation of "Frame Offset" / "First Frame Offset".</summary>
        public void SetFrameOffset(Int32 direction, Int32 index, Int32 x, Int32 y, Boolean inherit)
        {
            var dir = Dir(direction);
            var initial = GetFrameOffset(direction, index);
            Boolean changed = initial.X != x || initial.Y != y;

            if (index <= 0)
            {
                if (changed)
                    Project.Undo.Push(new UndoRecord((Int16)direction, 0, dir.FirstFrameOffsetX, dir.FirstFrameOffsetY, false));
                dir.FirstFrameOffsetX = x;
                dir.FirstFrameOffsetY = y;
                return;
            }

            var prev = dir.Frames[index - 1];
            var cur = dir.Frames[index];
            Int32 oldX = cur.OffsetX, oldY = cur.OffsetY;
            if (changed)
                Project.Undo.Push(new UndoRecord((Int16)direction, index, oldX, oldY, inherit));
            FrameMath.SetRelative(prev, cur, x, y);
            if (inherit)
                Propagate(dir, index, cur.OffsetX - oldX, cur.OffsetY - oldY);
        }

        private static void Propagate(Direction dir, Int32 fromIndex, Int32 dx, Int32 dy)
        {
            for (Int32 i = fromIndex + 1; i < dir.Count; i++)
            {
                dir.Frames[i].OffsetX += dx;
                dir.Frames[i].OffsetY += dy;
            }
        }

        /// <summary>
        /// Offset wizard (0x40f754): returns the Y value such that the total movement of the animation (from the first
        /// to the last frame) follows the direction's axis for the abscissa <paramref name="x"/> (truncated to 16 bits).
        /// </summary>
        public Int32 OffsetWizard(Int32 direction, Int32 x)
        {
            var dir = Dir(direction);
            var first = dir.Frames[0];
            var last = dir.Frames[^1];
            FrameMath.AutoCut(Project, first);
            FrameMath.AutoCut(Project, last);
            Int32 dx = FrameMath.PositionX(last) - FrameMath.PositionX(first);
            Int32 dy = FrameMath.PositionY(last) - FrameMath.PositionY(first);
            Double tan = DirectionTangent(direction);
            return Round(((Int16)x + dx) * tan - dy);
        }

        // ------------------------------------------------------------------ mouse ("Move" tool)

        private Boolean _pendingUndo;

        /// <summary>Start of a drag (<c>MouseDown</c> with the Move tool): arms the undo recording.</summary>
        public void BeginDrag() => _pendingUndo = true;

        /// <summary>Shift constraint: <c>y = anchorY + (x − anchorX) · tan(angle[dir])</c> rounded (0x40ef0c).</summary>
        public static Int32 ConstrainY(Int32 direction, Int32 anchorX, Int32 anchorY, Int32 x)
            => Round(anchorY + (x - anchorX) * DirectionTangent(direction));

        /// <summary>
        /// One-step move (<c>MouseMove</c>). <paramref name="selected"/> = selected frame. Frame 0: direction shift;
        /// frame &gt; 0: frame offset, unless <c>LockOffsets</c>. <paramref name="ctrl"/> propagates to the
        /// following frames.
        /// </summary>
        public void DragMove(Int32 direction, Int32 selected, Int32 dx, Int32 dy, Boolean ctrl)
        {
            var dir = Dir(direction);
            if (dir.Count == 0)
                return;
            if (selected <= 0)
            {
                if (_pendingUndo && (dx != 0 || dy != 0))
                {
                    Project.Undo.Push(new UndoRecord((Int16)direction, -1, dir.ShiftBaseX, dir.ShiftBaseY, false));
                    _pendingUndo = false;
                }
                dir.ShiftBaseX += dx;
                dir.ShiftBaseY += dy;
                return;
            }
            if (Project.Settings.LockOffsets)
                return;

            var f = dir.Frames[selected];
            if (_pendingUndo && (dx != 0 || dy != 0))
            {
                Project.Undo.Push(new UndoRecord((Int16)direction, selected, f.OffsetX, f.OffsetY, ctrl));
                _pendingUndo = false;
            }
            f.OffsetX += dx;
            f.OffsetY += dy;
            if (ctrl)
                Propagate(dir, selected, dx, dy);
        }

        // ------------------------------------------------------------------ undo

        public Boolean CanUndo => !Project.Undo.IsEmpty;

        /// <summary>Undoes the last offset change (0x40c5b4). Returns the affected direction, or −1.</summary>
        public Int32 Undo()
        {
            if (Project.Undo.IsEmpty)
                return -1;
            var r = Project.Undo.Pop();
            var dir = Dir(r.Direction);
            if (r.FrameIndex < 0)
            {
                dir.ShiftBaseX = r.X;
                dir.ShiftBaseY = r.Y;
            }
            else if (r.FrameIndex == 0)
            {
                dir.FirstFrameOffsetX = r.X;
                dir.FirstFrameOffsetY = r.Y;
            }
            else
            {
                var f = dir.Frames[r.FrameIndex];
                if (r.Inherit)
                    Propagate(dir, r.FrameIndex, r.X - f.OffsetX, r.Y - f.OffsetY);
                f.OffsetX = r.X;
                f.OffsetY = r.Y;
            }
            return r.Direction;
        }
    }
}