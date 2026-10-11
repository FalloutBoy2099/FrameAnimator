namespace FrameAnimator.Core.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Undo record (0x14 bytes in the original): saves the OLD value of an offset.
    /// <list type="bullet">
    /// <item><c>FrameIndex &lt; 0</c>: <c>ShiftBase</c> of the direction (X,Y).</item>
    /// <item><c>FrameIndex == 0</c>: <c>FirstFrameOffset</c> of the direction.</item>
    /// <item><c>FrameIndex &gt; 0</c>: absolute offset of the frame; <c>Inherit</c> propagates the difference to the following frames.</item>
    /// </list>
    /// </summary>
    public readonly record struct UndoRecord(Int16 Direction, Int32 FrameIndex, Int32 X, Int32 Y, Boolean Inherit);

    /// <summary>Stack limited to 10 records (the oldest is dropped when the 11th is pushed).</summary>
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