
using System.Threading;
using EditorUtils.Implementation.Utilities;
namespace EditorUtils.Implementation.Tagging
{
    internal sealed partial class AsyncTagger<TData, TTag>
    {
        /// <summary>
        /// This class is used to support the one way transfer of SnapshotLineRange values between
        /// the foreground thread of the tagger and the background processing thread.  It understands
        /// the priority placed on the visible UI lines and will transfer those lines at a higher
        /// priority than normal requests
        /// </summary>
        [UsedInBackgroundThread]
        internal sealed class Channel
        {
            /// <summary>
            /// Need another type here because SnapshotLineRange is a struct and we need atomic assignment
            /// guarantees to use Interlocked.Exchange
            /// </summary>
            private sealed class TextViewLineRange
            {
                public readonly SnapshotLineRange LineRange;
                internal TextViewLineRange(SnapshotLineRange lineRange)
                {
                    LineRange = lineRange;
                }
            }

            /// <summary>
            /// This is the normal request stack from the main thread.  More recently requested items
            /// are given higher priority than older items
            /// </summary>
            private ReadOnlyStack<SnapshotLineRange> _stack;

            /// <summary>
            /// When set this is represents the visible line range of the text view.  It has the highest
            /// priority for the background thread
            /// </summary>
            private TextViewLineRange _textViewLineRange;

            /// <summary>
            /// Version number tracks the number of writes to the channel
            /// </summary>
            private int _version;

            /// <summary>
            /// The current state of the request stack
            /// </summary>
            internal ReadOnlyStack<SnapshotLineRange> CurrentStack
            {
                get { return _stack; }
            }

            /// <summary>
            /// This number is incremented after every write to the channel.  It is a hueristic only and 
            /// not an absolute indicator.  It is not set atomically with every write but instead occurs
            /// some time after the write.  
            /// </summary>
            internal int CurrentVersion
            {
                get { return _version; }
            }

            internal Channel()
            {
                _stack = ReadOnlyStack<SnapshotLineRange>.Empty;
            }

            internal void WriteVisibleLines(SnapshotLineRange lineRange)
            { 
                var textViewLineRange = new TextViewLineRange(lineRange);
                Interlocked.Exchange(ref _textViewLineRange, textViewLineRange);
                Interlocked.Increment(ref _version);
            }

            internal void WriteNormal(SnapshotLineRange lineRange)
            {
                bool success;
                do
                {
                    var oldStack = _stack;
                    var newStack = _stack.Push(lineRange);
                    success = oldStack == Interlocked.CompareExchange(ref _stack, newStack, oldStack);
                } while (!success);

                Interlocked.Increment(ref _version);
            }

            internal SnapshotLineRange? Read()
            {
                var lineRange = ReadVisibleLines();
                if (lineRange.HasValue)
                {
                    return lineRange;
                }

                return ReadNormal();
            }

            private SnapshotLineRange? ReadVisibleLines()
            {
                do
                {
                    var oldTextViewLineRange = _textViewLineRange;
                    if (oldTextViewLineRange == null)
                    {
                        return null;
                    }

                    var success = oldTextViewLineRange == Interlocked.CompareExchange(ref _textViewLineRange, null, oldTextViewLineRange);
                    if (success)
                    {
                        return oldTextViewLineRange.LineRange;
                    }
                }
                while (true);
            }

            private SnapshotLineRange? ReadNormal()
            {
                do
                {
                    var oldStack = _stack;
                    if (oldStack.IsEmpty)
                    {
                        return null;
                    }

                    var newStack = _stack.Pop();
                    var success = oldStack == Interlocked.CompareExchange(ref _stack, newStack, oldStack);
                    if (success)
                    {
                        return oldStack.Value;
                    }
                } while (true);
            }
        }
    }
}
