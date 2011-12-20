using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class LineRangeTest
    {
        /// <summary>
        /// Set of not intersecting ranges
        /// </summary>
        [Test]
        public void Intersects_SimpleDoesnt()
        {
            var left = LineRange.CreateFromBounds(0, 1);
            var right = LineRange.CreateFromBounds(3, 4);
            Assert.IsFalse(left.Intersects(right));
        }

        /// <summary>
        /// Set of intersecting ranges
        /// </summary>
        [Test]
        public void Intersects_SimpleDoes()
        {
            var left = LineRange.CreateFromBounds(0, 2);
            var right = LineRange.CreateFromBounds(1, 4);
            Assert.IsTrue(left.Intersects(right));
        }

        /// <summary>
        /// The intersect if they have the same boundary lines (essentially if they touch
        /// each other)
        /// </summary>
        [Test]
        public void Intersects_DoesAtBorder()
        {
            var left = LineRange.CreateFromBounds(0, 2);
            var right = LineRange.CreateFromBounds(3, 4);
            Assert.IsTrue(left.Intersects(right));
        }
    }
}
