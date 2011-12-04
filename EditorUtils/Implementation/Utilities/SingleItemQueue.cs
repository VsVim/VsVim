using System.Threading;

namespace EditorUtils.Implementation.Utilities
{
    /// <summary>
    /// A queue like implementation for a single item which is shared amongst
    /// multiple threads
    /// </summary>
    [UsedInBackgroundThread]
    internal sealed class SingleItemQueue<T>
    {
        sealed class Item
        {
            internal readonly bool IsEmpty;
            internal readonly T Value;
            internal Item(bool isEmpty, T value)
            {
                IsEmpty = isEmpty;
                Value = value;
            }
        }

        private Item _item;

        internal void Enqueue(T value)
        {
            Interlocked.Exchange(ref _item, new Item(false, value));
        }

        internal bool TryDequeue(out T value)
        {
            var item = Interlocked.Exchange(ref _item, new Item(true, default(T)));
            value = item.Value;
            return !item.IsEmpty;
        }
    }
}
