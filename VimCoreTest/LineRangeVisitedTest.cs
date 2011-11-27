using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class LineRangeVisitedTest
    {
        /// <summary>
        /// Count the number of contiguous ranges which is backing the LineRangeVisited.  Useful for 
        /// guaranteeing that we are collapsing properly
        /// </summary>
        private static int CountLineRanges(LineRangeVisited lineRangeVisited)
        {
            if (lineRangeVisited.IsContiguous)
            {
                return 1;
            }

            var discontiguous = lineRangeVisited.AsDiscontiguous();
            return CountLineRanges(discontiguous.Item1) + CountLineRanges(discontiguous.Item2);
        }

        /// <summary>
        /// Validate tha the LineRangeVisited structure holds the contracts we'd like to guarantee for
        /// it
        /// </summary>
        private static void AssertValid(LineRangeVisited lineRangeVisited)
        {
            if (lineRangeVisited.IsContiguous)
            {
                return;
            }

            var discontiguous = lineRangeVisited.AsDiscontiguous();
            var left = discontiguous.Item1;
            var right = discontiguous.Item2;
            if (left.IsContiguous && right.IsContiguous && left.LineRange.Intersects(right.LineRange))
            {
                Assert.Fail("A discontiguous region of two contiguous regions shouldn't intersect (or they could be collapsed");
            }

            if (left.LastLineNumber > right.StartLineNumber)
            {
                Assert.Fail("Left should be before right");
            }

            if (left.StartLineNumber > right.LastLineNumber)
            {
                Assert.Fail("Left should be before right");
            }

            AssertValid(left);
            AssertValid(right);
        }

        /// <summary>
        /// Make sure we get the proper start line for a simple contiguous region
        /// </summary>
        [Test]
        public void LineRange_Contiguous()
        {
            var lineRange = new LineRange(1, 2);
            var visited = LineRangeVisited.NewContiguous(new LineRange(1, 2));
            Assert.AreEqual(lineRange, visited.LineRange);
        }

        /// <summary>
        /// Make sure we get the proper LineRange for a discontiguous 
        /// </summary>
        [Test]
        public void LineRange_Discontiguous()
        {
            var lineRange1 = new LineRange(1, 2);
            var lineRange2 = new LineRange(10, 3);
            var visited = LineRangeVisited.NewDiscontiguous(
                LineRangeVisited.NewContiguous(lineRange1),
                LineRangeVisited.NewContiguous(lineRange2));
            Assert.AreEqual(LineRange.CreateOverarching(lineRange1, lineRange2), visited.LineRange);
        }

        /// <summary>
        /// When adding an intersecting LineRange to a Contiguous value we should collapse the 
        /// values
        /// </summary>
        [Test]
        public void AddLineRange_Intesects_ToContiguous()
        {
            var visited = LineRangeVisited.NewContiguous(new LineRange(1, 4));
            visited = visited.AddLineRange(new LineRange(3, 3));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(1, 5), visited.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// Not intersecting ranges should create a discontiguous block
        /// </summary>
        [Test]
        public void AddLineRange_NotIntersects_ToContiguous()
        {
            var visited = LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(0, 2));
            visited = visited.AddLineRange(LineRange.CreateFromBounds(4, 6));
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 6), visited.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// If there is a discontiguous region and we add the missing link it should
        /// collapse into a simple contiguous one
        /// </summary>
        [Test]
        public void AddLineRange_CollapseDiscontiguousRegion()
        {
            var visited = LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(0, 1));
            visited = visited.AddLineRange(LineRange.CreateFromBounds(3, 4));
            Assert.IsTrue(visited.IsDiscontiguous);
            visited = visited.AddLineRange(LineRange.CreateFromBounds(2, 2));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// If we have V (D (C , C)) (C) and the new LineRange intersects all of them then we
        /// should end up with a single contiguous region
        /// </summary>
        [Test]
        public void AddLineRange_CollapseCascade_OnLeft()
        {
            var visited = LineRangeVisited.NewDiscontiguous(
                LineRangeVisited.NewDiscontiguous(
                    LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(0, 1)),
                    LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(3, 4))),
                LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(6, 7)));
            AssertValid(visited);
            visited = visited.AddLineRange(LineRange.CreateFromBounds(1, 6));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 7), visited.LineRange);
        }
        /// <summary>
        /// If we have V C (D (C , C)) and the new LineRange intersects all of them then we
        /// should end up with a single contiguous region
        /// </summary>
        [Test]
        public void AddLineRange_CollapseCascade_OnRight()
        {
            var visited = LineRangeVisited.NewDiscontiguous(
                LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(0, 1)),
                LineRangeVisited.NewDiscontiguous(
                    LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(3, 4)),
                    LineRangeVisited.NewContiguous(LineRange.CreateFromBounds(6, 7))));
            AssertValid(visited);
            visited = visited.AddLineRange(LineRange.CreateFromBounds(1, 6));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 7), visited.LineRange);
        }

        /// <summary>
        /// Test D (C C) where the LineRange fills the gap between the 2 C regions
        /// </summary>
        [Test]
        public void AddLineRange_FillsGapBetweenContiguous()
        {
            var visited = LineRangeVisited.OfSeq(new[] {
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4) }).Value;
            AssertValid(visited);
            visited = visited.AddLineRange(LineRange.CreateFromBounds(2, 2));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// Make sure we create a proper structured from a set of non-intersecting values
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting()
        {
            var visited = LineRangeVisited.OfSeq(new[] {
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// Make sure the structure is correct when the values aren't properly ordered
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting_WrongOrder()
        {
            var visited = LineRangeVisited.OfSeq(new[] {
                LineRange.CreateFromBounds(3, 4),
                LineRange.CreateFromBounds(0, 1) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            AssertValid(visited);
        }

        /// <summary>
        /// Make sure the structure is correct when the values intersect
        /// </summary>
        [Test]
        public void OfSeq_Intersecting_WrongOrder()
        {
            var visited = LineRangeVisited.OfSeq(new[] {
                LineRange.CreateFromBounds(3, 3),
                LineRange.CreateFromBounds(4, 4),
                LineRange.CreateFromBounds(0, 1) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            AssertValid(visited);
        }
    }
}
