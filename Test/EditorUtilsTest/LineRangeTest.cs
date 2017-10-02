using Xunit;
using System;

namespace EditorUtils.UnitTest
{
    public abstract class LineRangeTest
    {
        public sealed class IntersectsTest : LineRangeTest
        {
            /// <summary>
            /// Set of not intersecting ranges
            /// </summary>
            [Fact]
            public void Intersects_SimpleDoesnt()
            {
                var left = LineRange.CreateFromBounds(0, 1);
                var right = LineRange.CreateFromBounds(3, 4);
                Assert.False(left.Intersects(right));
            }

            /// <summary>
            /// Set of intersecting ranges
            /// </summary>
            [Fact]
            public void Intersects_SimpleDoes()
            {
                var left = LineRange.CreateFromBounds(0, 2);
                var right = LineRange.CreateFromBounds(1, 4);
                Assert.True(left.Intersects(right));
            }

            /// <summary>
            /// The intersect if they have the same boundary lines (essentially if they touch
            /// each other)
            /// </summary>
            [Fact]
            public void Intersects_DoesAtBorder()
            {
                var left = LineRange.CreateFromBounds(0, 2);
                var right = LineRange.CreateFromBounds(3, 4);
                Assert.True(left.Intersects(right));
            }
        }

        public sealed class CreateFromBoundsTest : LineRangeTest
        {
            [Fact]
            public void Simple()
            {
                var lineRange = LineRange.CreateFromBounds(1, 3);
                Assert.Equal(3, lineRange.Count);
                Assert.Equal(1, lineRange.StartLineNumber);
                Assert.Equal(3, lineRange.LastLineNumber);
            }

            [Fact]
            public void BadBounds()
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => LineRange.CreateFromBounds(3, 1));
            }
        }

        public sealed class EqualityTest : LineRangeTest
        {
            void Run(EqualityUnit<LineRange> equalityUnit)
            {
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    equalityUnit);
            }

            [Fact]
            public void Test1()
            {
                var equalityUnit = EqualityUnit
                    .Create(new LineRange(1, 1))
                    .WithEqualValues(new LineRange(1, 1))
                    .WithNotEqualValues(new LineRange(2, 1), new LineRange(1, 3));
                Run(equalityUnit);
            }

            [Fact]
            public void Test2()
            {
                var equalityUnit = EqualityUnit
                    .Create(new LineRange(2, 3))
                    .WithEqualValues(new LineRange(2, 3))
                    .WithNotEqualValues(new LineRange(2, 1), new LineRange(1, 3));
                Run(equalityUnit);
            }
        }
    }
}
