namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Geometry;
    using Model;

    /// <summary>
    /// Opérations d'édition (indépendantes de l'interface) avec gestion de l'annulation, reprenant la logique des
    /// gestionnaires <c>acFramesOffset</c>, <c>acFrameOffset</c>, <c>acOffsetWizard</c>, souris et <c>acUndo</c> de l'original.
    /// </summary>
    public sealed class ProjectEditor(Project project)
    {
        public Project Project { get; } = project;

        /// <summary>Angles de déplacement (en degrés) par direction, utilisés par la contrainte Maj et l'assistant (0x40ef6d…).</summary>
        public static readonly Double[] DirectionAngles = [-36.9, 0.0, 36.9, 143.1, 180.0, -143.1];

        public static Double DirectionTangent(Int32 direction) => Math.Tan(DirectionAngles[direction] * (Math.PI / 180.0));

        private Direction Dir(Int32 d) => Project.Directions[d];

        private static Int32 Round(Double v) => (Int32)Math.Round(v, MidpointRounding.ToEven);

        // ------------------------------------------------------------------ frames

        /// <summary>« Frame Down / Up » (0x40c730) : déplace la frame <paramref name="index"/> de <paramref name="delta"/> et vide l'annulation.</summary>
        public void MoveFrame(Int32 direction, Int32 index, Int32 delta)
        {
            var frames = Dir(direction).Frames;
            Int32 target = index + delta;
            var f = frames[index];
            frames.RemoveAt(index);
            frames.Insert(target, f);
            Project.Undo.Clear();
        }

        /// <summary>« Remove Frames » : supprime les indices donnés (dans n'importe quel ordre) et vide l'annulation.</summary>
        public void RemoveFrames(Int32 direction, IEnumerable<Int32> indices)
        {
            var frames = Dir(direction).Frames;
            foreach (Int32 i in indices.Distinct().OrderByDescending(x => x))
                if (i >= 0 && i < frames.Count)
                    frames.RemoveAt(i);
            Project.Undo.Clear();
        }

        // ------------------------------------------------------------------ décalages (boîtes de dialogue)

        /// <summary>Valeurs initiales de « Frames Offset » : <c>GetShift</c> après auto‑coupe de la frame 0.</summary>
        public (Int32 X, Int32 Y) GetFramesOffset(Int32 direction)
        {
            var dir = Dir(direction);
            FrameMath.AutoCut(Project, dir.Frames[0]);
            return FrameMath.GetShift(dir);
        }

        /// <summary>Validation de « Frames Offset » : annulation si changement puis <c>SetShift</c>.</summary>
        public void SetFramesOffset(Int32 direction, Int32 x, Int32 y)
        {
            var dir = Dir(direction);
            var initial = GetFramesOffset(direction);
            if (initial.X != x || initial.Y != y)
                Project.Undo.Push(new UndoRecord((Int16)direction, -1, dir.ShiftBaseX, dir.ShiftBaseY, false));
            FrameMath.SetShift(dir, x, y);
        }

        /// <summary>
        /// Valeurs initiales de « Frame Offset » / « First Frame Offset » : relatif à la frame précédente pour
        /// <paramref name="index"/> &gt; 0, <c>FirstFrameOffset</c> sinon.
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

        /// <summary>Validation de « Frame Offset » / « First Frame Offset ».</summary>
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
        /// Assistant d'offset (0x40f754) : retourne la valeur de Y telle que le déplacement total de l'animation (de la première
        /// à la dernière frame) suive l'axe de la direction pour l'abscisse <paramref name="x"/> (tronquée sur 16 bits).
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

        // ------------------------------------------------------------------ souris (outil « Move »)

        private Boolean _pendingUndo;

        /// <summary>Début d'un glisser (<c>MouseDown</c> avec l'outil Move) : arme l'enregistrement d'annulation.</summary>
        public void BeginDrag() => _pendingUndo = true;

        /// <summary>Contrainte Maj : <c>y = ancreY + (x − ancreX) · tan(angle[dir])</c> arrondi (0x40ef0c).</summary>
        public static Int32 ConstrainY(Int32 direction, Int32 anchorX, Int32 anchorY, Int32 x)
            => Round(anchorY + (x - anchorX) * DirectionTangent(direction));

        /// <summary>
        /// Déplacement d'un cran (<c>MouseMove</c>). <paramref name="selected"/> = frame sélectionnée. Frame 0 : décalage de
        /// direction ; frame &gt; 0 : décalage de la frame, sauf si <c>LockOffsets</c>. <paramref name="ctrl"/> propage aux
        /// frames suivantes.
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

        // ------------------------------------------------------------------ annulation

        public Boolean CanUndo => !Project.Undo.IsEmpty;

        /// <summary>Annule le dernier changement de décalage (0x40c5b4). Retourne la direction concernée ou −1.</summary>
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