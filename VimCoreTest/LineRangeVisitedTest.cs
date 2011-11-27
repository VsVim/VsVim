using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class LineRangeVisitedTest
    {
        [Test]
        public void Add_Simple()
        {
            var visited = new LineRangeVisited();
            visited.Add(LineRange.CreateFromBounds(0, 2));
            Assert.AreEqual(LineRange.CreateFromBounds(0, 2), visited.LineRange.Value);
        }
    }
}
