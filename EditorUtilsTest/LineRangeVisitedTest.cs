using EditorUtils.Implementation.Utilities;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class LineRangeVisitedTest
    {
        private LineRangeVisited Create(params LineRange[] lineRanges)
        {
            return new LineRangeVisited(lineRanges);
        }

        [Test]
        public void Add_Simple()
        {
            var visited = new LineRangeVisited();
            visited.Add(LineRange.CreateFromBounds(0, 2));
            Assert.AreEqual(LineRange.CreateFromBounds(0, 2), visited.LineRange.Value);
        }

        /// <summary>
        /// Adding a LineRange which intersects with the existing one shoud not cause any
        /// extra items to be added
        /// </summary>
        [Test]
        public void Add_Intersects()
        {
            var visited = Create(new LineRange(1, 4));
            visited.Add(LineRange.CreateFromBounds(3, 5));
            Assert.AreEqual(1, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(1, 5), visited.LineRange.Value);
        }

        /// <summary>
        /// Not intersecting ranges should cause multiple items to be in the List
        /// </summary>
        [Test]
        public void Add_NotIntersects()
        {
            var visited = Create(LineRange.CreateFromBounds(0, 2));
            visited.Add(LineRange.CreateFromBounds(4, 6));
            Assert.AreEqual(2, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 2), visited.List[0]);
            Assert.AreEqual(LineRange.CreateFromBounds(4, 6), visited.List[1]);
        }

        /// <summary>
        /// Not intersecting ranges should cause multiple items to be in the List
        /// </summary>
        [Test]
        public void Add_NotIntersects_ReverseOrder()
        {
            var visited = Create(LineRange.CreateFromBounds(4, 6));
            visited.Add(LineRange.CreateFromBounds(0, 2));
            Assert.AreEqual(2, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 2), visited.List[0]);
            Assert.AreEqual(LineRange.CreateFromBounds(4, 6), visited.List[1]);
        }

        /// <summary>
        /// If there is a discontiguous region and we add the missing link it should
        /// collapse into a simple contiguous one
        /// </summary>
        [Test]
        public void Add_MissingLineRange()
        {
            var visited = Create(LineRange.CreateFromBounds(0, 1));
            visited.Add(LineRange.CreateFromBounds(3, 4));
            Assert.AreEqual(2, visited.List.Count);
            visited.Add(LineRange.CreateFromBounds(2, 2));
            Assert.AreEqual(1, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange.Value);
        }

        /// <summary>
        /// If we have a gap of regions and Add one that intersects them all it should collapse 
        /// them
        /// </summary>
        [Test]
        public void Add_IntersectMultiple()
        {
            var visited = Create(
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4),
                LineRange.CreateFromBounds(6, 7));
            Assert.AreEqual(3, visited.List.Count);
            visited.Add(LineRange.CreateFromBounds(1, 6));
            Assert.AreEqual(1, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 7), visited.LineRange.Value);
        }

        /// <summary>
        /// Test D (C C) where the LineRange fills the gap between the 2 C regions
        /// </summary>
        [Test]
        public void Add_FillsGap()
        {
            var visited = Create(
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4));
            visited.Add(LineRange.CreateFromBounds(2, 2));
            Assert.AreEqual(1, visited.List.Count);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange.Value);
        }

        /// <summary>
        /// Make sure we create a proper structured from a set of non-intersecting values
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting()
        {
            var visited = Create(
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4));
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.List[0]);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.List[1]);
        }

        /// <summary>
        /// Make sure the structure is correct when the values aren't properly ordered
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting_WrongOrder()
        {
            var visited = Create(
                LineRange.CreateFromBounds(3, 4),
                LineRange.CreateFromBounds(0, 1));
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.List[0]);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.List[1]);
        }

        /// <summary>
        /// Make sure the structure is correct when the values intersect
        /// </summary>
        [Test]
        public void OfSeq_Intersecting_WrongOrder()
        {
            var visited = Create(
                LineRange.CreateFromBounds(3, 3),
                LineRange.CreateFromBounds(4, 4),
                LineRange.CreateFromBounds(0, 1));
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.List[0]);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.List[1]);
        }
    }
}
