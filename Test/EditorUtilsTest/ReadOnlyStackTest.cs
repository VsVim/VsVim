using System;
using System.Linq;
using EditorUtils.Implementation.Utilities;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class ReadOnlyStackTest
    {
        public sealed class PopTest : ReadOnlyStackTest
        {
            [Fact]
            public void EmptyThrows()
            {
                var stack = ReadOnlyStack<int>.Empty;
                Assert.Throws<Exception>(() => stack.Pop());
            }

            [Fact]
            public void Simple()
            {
                var stack = ReadOnlyStack<int>.Empty;
                stack = stack.Push(1);
                stack = stack.Pop();
                Assert.True(stack.IsEmpty);
                Assert.Equal(0, stack.Count);
            }
        }

        public sealed class PushTest : ReadOnlyStackTest
        {
            [Fact]
            public void EnumerateFirstInLastOut()
            {
                var stack = ReadOnlyStack<int>.Empty;
                for (int i = 0; i < 3; i++)
                {
                    stack = stack.Push(i);
                }

                Assert.Equal(new[] { 2, 1, 0 }, stack.ToList());
            }
        }
    }
}
