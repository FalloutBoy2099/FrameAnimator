namespace FrameAnimator.Core.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Enregistrement d'annulation (0x14 octets dans l'original) : sauvegarde de l'ANCIENNE valeur d'un décalage.
    /// <list type="bullet">
    /// <item><c>FrameIndex &lt; 0</c> : <c>ShiftBase</c> de la direction (X,Y).</item>
    /// <item><c>FrameIndex == 0</c> : <c>FirstFrameOffset</c> de la direction.</item>
    /// <item><c>FrameIndex &gt; 0</c> : décalage absolu de la frame ; <c>Inherit</c> propage la différence aux frames suivantes.</item>
    /// </list>
    /// </summary>
    public readonly record struct UndoRecord(Int16 Direction, Int32 FrameIndex, Int32 X, Int32 Y, Boolean Inherit);

    /// <summary>Pile bornée à 10 enregistrements (la plus ancienne est supprimée au 11e).</summary>
    public sealed class UndoStack
    {
        public const Int32 Capacity = 10;

        private readonly List<UndoRecord> _items = [];

        public Int32 Count => _items.Count;
        public Boolean IsEmpty => _items.Count == 0;

        public void Push(UndoRecord record)
        {
            if (_items.Count == Capacity)
                _items.RemoveAt(0);
            _items.Add(record);
        }

        public UndoRecord Pop()
        {
            var r = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            return r;
        }

        public void Clear() => _items.Clear();
    }
}