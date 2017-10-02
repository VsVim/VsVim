using EditorUtils.Implementation.Utilities;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class NormalizedLineRangeCollectionTest
    {
        internal NormalizedLineRangeCollection Create(params LineRange[] lineRanges)
        {
            return new NormalizedLineRangeCollection(lineRanges);
        }

        public sealed class AddTest : NormalizedLineRangeCollectionTest
        {
            [Fact]
            public void Simple()
            {
                var visited = new NormalizedLineRangeCollection();
                visited.Add(LineRange.CreateFromBounds(0, 2));
                Assert.Equal(LineRange.CreateFromBounds(0, 2), visited.OverarchingLineRange.Value);
            }

            /// <summary>
            /// Adding a LineRange which intersects with the existing one shoud not cause any
            /// extra items to be added
            /// </summary>
            [Fact]
            public void Intersects()
            {
                var visited = Create(new LineRange(1, 4));
                visited.Add(LineRange.CreateFromBounds(3, 5));
                Assert.Equal(1, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(1, 5), visited.OverarchingLineRange.Value);
            }

            /// <summary>
            /// Not intersecting ranges should cause multiple items to be in the List
            /// </summary>
            [Fact]
            public void NotIntersects()
            {
                var visited = Create(LineRange.CreateFromBounds(0, 2));
                visited.Add(LineRange.CreateFromBounds(4, 6));
                Assert.Equal(2, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 2), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(4, 6), visited[1]);
            }

            /// <summary>
            /// Not intersecting ranges should cause multiple items to be in the List
            /// </summary>
            [Fact]
            public void NotIntersects_ReverseOrder()
            {
                var visited = Create(LineRange.CreateFromBounds(4, 6));
                visited.Add(LineRange.CreateFromBounds(0, 2));
                Assert.Equal(2, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 2), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(4, 6), visited[1]);
            }

            /// <summary>
            /// If there is a discontiguous region and we add the missing link it should
            /// collapse into a simple contiguous one
            /// </summary>
            [Fact]
            public void MissingLineRange()
            {
                var visited = Create(LineRange.CreateFromBounds(0, 1));
                visited.Add(LineRange.CreateFromBounds(3, 4));
                Assert.Equal(2, visited.Count);
                visited.Add(LineRange.CreateFromBounds(2, 2));
                Assert.Equal(1, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 4), visited.OverarchingLineRange.Value);
            }

            /// <summary>
            /// If we have a gap of regions and Add one that intersects them all it should collapse 
            /// them
            /// </summary>
            [Fact]
            public void IntersectMultiple()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(0, 1),
                    LineRange.CreateFromBounds(3, 4),
                    LineRange.CreateFromBounds(6, 7));
                Assert.Equal(3, visited.Count);
                visited.Add(LineRange.CreateFromBounds(1, 6));
                Assert.Equal(1, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 7), visited.OverarchingLineRange.Value);
            }

            /// <summary>
            /// The case where the LineRange fits between 2 existing items according to the start
            /// line but actually intersects the first item
            /// </summary>
            [Fact]
            public void IntesectBefore()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(0, 3),
                    LineRange.CreateFromBounds(6, 7));
                Assert.Equal(2, visited.Count);
                visited.Add(LineRange.CreateFromBounds(2, 4));
                Assert.Equal(2, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 4), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(6, 7), visited[1]);
            }

            /// <summary>
            /// The case where this fits behind the last element in the collection according to the
            /// start line but actually intersects the last item
            /// </summary>
            [Fact]
            public void IntersectLast()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(0, 3),
                    LineRange.CreateFromBounds(6, 9));
                visited.Add(LineRange.CreateFromBounds(7, 10));
                Assert.Equal(
                    new[] { LineRange.CreateFromBounds(0, 3), LineRange.CreateFromBounds(6, 10) },
                    visited);
            }

            /// <summary>
            /// Test D (C C) where the LineRange fills the gap between the 2 C regions
            /// </summary>
            [Fact]
            public void FillsGap()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(0, 1),
                    LineRange.CreateFromBounds(3, 4));
                visited.Add(LineRange.CreateFromBounds(2, 2));
                Assert.Equal(1, visited.Count);
                Assert.Equal(LineRange.CreateFromBounds(0, 4), visited.OverarchingLineRange.Value);
            }
        }

        public sealed class OfSeqTest : NormalizedLineRangeCollectionTest
        {
            /// <summary>
            /// Make sure we create a proper structured from a set of non-intersecting values
            /// </summary>
            [Fact]
            public void NonIntersecting()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(0, 1),
                    LineRange.CreateFromBounds(3, 4));
                Assert.Equal(LineRange.CreateFromBounds(0, 1), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(3, 4), visited[1]);
            }

            /// <summary>
            /// Make sure the structure is correct when the values aren't properly ordered
            /// </summary>
            [Fact]
            public void NonIntersecting_WrongOrder()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(3, 4),
                    LineRange.CreateFromBounds(0, 1));
                Assert.Equal(LineRange.CreateFromBounds(0, 1), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(3, 4), visited[1]);
            }

            /// <summary>
            /// Make sure the structure is correct when the values intersect
            /// </summary>
            [Fact]
            public void Intersecting_WrongOrder()
            {
                var visited = Create(
                    LineRange.CreateFromBounds(3, 3),
                    LineRange.CreateFromBounds(4, 4),
                    LineRange.CreateFromBounds(0, 1));
                Assert.Equal(LineRange.CreateFromBounds(0, 1), visited[0]);
                Assert.Equal(LineRange.CreateFromBounds(3, 4), visited[1]);
            }
        }
    }

    public sealed class MiscTest : NormalizedLineRangeCollectionTest
    {
        [Fact]
        public void Clear()
        {
            var col = new NormalizedLineRangeCollection();
            col.Add(new LineRange(1, 2));
            col.Clear();
            Assert.Equal(0, col.Count);
        }
    }
}
