using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class LineRangeVisitedNodeTest
    {
        /// <summary>
        /// Make sure we get the proper start line for a simple contiguous region
        /// </summary>
        [Test]
        public void LineRange_Contiguous()
        {
            var lineRange = new LineRange(1, 2);
            var visited = LineRangeVisitedNode.NewContiguous(new LineRange(1, 2));
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
            var visited = LineRangeVisitedNode.NewDiscontiguous(
                LineRangeVisitedNode.NewContiguous(lineRange1),
                LineRangeVisitedNode.NewContiguous(lineRange2));
            Assert.AreEqual(LineRange.CreateOverarching(lineRange1, lineRange2), visited.LineRange);
        }

        /// <summary>
        /// When adding an intersecting LineRange to a Contiguous value we should collapse the 
        /// values
        /// </summary>
        [Test]
        public void Add_Intesects_ToContiguous()
        {
            var visited = LineRangeVisitedNode.NewContiguous(new LineRange(1, 4));
            visited = visited.Add(new LineRange(3, 3));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(1, 5), visited.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// Not intersecting ranges should create a discontiguous block
        /// </summary>
        [Test]
        public void Add_NotIntersects_ToContiguous()
        {
            var visited = LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(0, 2));
            visited = visited.Add(LineRange.CreateFromBounds(4, 6));
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 6), visited.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// If there is a discontiguous region and we add the missing link it should
        /// collapse into a simple contiguous one
        /// </summary>
        [Test]
        public void Add_CollapseDiscontiguousRegion()
        {
            var visited = LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(0, 1));
            visited = visited.Add(LineRange.CreateFromBounds(3, 4));
            Assert.IsTrue(visited.IsDiscontiguous);
            visited = visited.Add(LineRange.CreateFromBounds(2, 2));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// If we have V (D (C , C)) (C) and the new LineRange intersects all of them then we
        /// should end up with a single contiguous region
        /// </summary>
        [Test]
        public void Add_CollapseCascade_OnLeft()
        {
            var visited = LineRangeVisitedNode.NewDiscontiguous(
                LineRangeVisitedNode.NewDiscontiguous(
                    LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(0, 1)),
                    LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(3, 4))),
                LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(6, 7)));
            visited.AssertValid();
            visited = visited.Add(LineRange.CreateFromBounds(1, 6));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 7), visited.LineRange);
        }
        /// <summary>
        /// If we have V C (D (C , C)) and the new LineRange intersects all of them then we
        /// should end up with a single contiguous region
        /// </summary>
        [Test]
        public void Add_CollapseCascade_OnRight()
        {
            var visited = LineRangeVisitedNode.NewDiscontiguous(
                LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(0, 1)),
                LineRangeVisitedNode.NewDiscontiguous(
                    LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(3, 4)),
                    LineRangeVisitedNode.NewContiguous(LineRange.CreateFromBounds(6, 7))));
            visited.AssertValid();
            visited = visited.Add(LineRange.CreateFromBounds(1, 6));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 7), visited.LineRange);
        }

        /// <summary>
        /// Test D (C C) where the LineRange fills the gap between the 2 C regions
        /// </summary>
        [Test]
        public void Add_FillsGapBetweenContiguous()
        {
            var visited = LineRangeVisitedNode.OfSeq(new[] {
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4) }).Value;
            visited.AssertValid();
            visited = visited.Add(LineRange.CreateFromBounds(2, 2));
            Assert.IsTrue(visited.IsContiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 4), visited.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// Make sure we create a proper structured from a set of non-intersecting values
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting()
        {
            var visited = LineRangeVisitedNode.OfSeq(new[] {
                LineRange.CreateFromBounds(0, 1),
                LineRange.CreateFromBounds(3, 4) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// Make sure the structure is correct when the values aren't properly ordered
        /// </summary>
        [Test]
        public void OfSeq_NonIntersecting_WrongOrder()
        {
            var visited = LineRangeVisitedNode.OfSeq(new[] {
                LineRange.CreateFromBounds(3, 4),
                LineRange.CreateFromBounds(0, 1) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            visited.AssertValid();
        }

        /// <summary>
        /// Make sure the structure is correct when the values intersect
        /// </summary>
        [Test]
        public void OfSeq_Intersecting_WrongOrder()
        {
            var visited = LineRangeVisitedNode.OfSeq(new[] {
                LineRange.CreateFromBounds(3, 3),
                LineRange.CreateFromBounds(4, 4),
                LineRange.CreateFromBounds(0, 1) }).Value;
            Assert.IsTrue(visited.IsDiscontiguous);
            Assert.AreEqual(LineRange.CreateFromBounds(0, 1), visited.AsDiscontiguous().Item1.LineRange);
            Assert.AreEqual(LineRange.CreateFromBounds(3, 4), visited.AsDiscontiguous().Item2.LineRange);
            visited.AssertValid();
        }
    }
}
