using System;
using System.Collections.Generic;

namespace EditorUtils.Implementation.Utilities
{
    /// <summary>
    /// Make this a ReadOnly stack
    /// </summary>
    [UsedInBackgroundThread]
    internal sealed class ReadOnlyStack<T> : IEnumerable<T>
    {
        internal static readonly ReadOnlyStack<T> Empty = new ReadOnlyStack<T>();

        private readonly ReadOnlyStack<T> _next;
        private readonly T _value;
        private readonly int _count;

        internal bool IsEmpty
        {
            get { return _next == null; }
        }

        internal int Count
        {
            get { return _count; }
        }

        internal T Value
        {
            get
            {
                ThrowIfEmpty();
                return _value;
            }
        }

        private ReadOnlyStack()
        {

        }

        private ReadOnlyStack(T lineRange, ReadOnlyStack<T> next)
        {
            _value = lineRange;
            _next = next;
            _count = next.Count + 1;
        }

        internal ReadOnlyStack<T> Pop()
        {
            ThrowIfEmpty();
            return _next;
        }

        internal ReadOnlyStack<T> Push(T lineRange)
        {
            return new ReadOnlyStack<T>(lineRange, this);
        }

        private void ThrowIfEmpty()
        {
            if (IsEmpty)
            {
                throw new Exception("Stack is empty");
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            var top = this;
            while (!top.IsEmpty)
            {
                yield return top.Value;
                top = top.Pop();
            }
        }

        #region IEnumerable<T>

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
