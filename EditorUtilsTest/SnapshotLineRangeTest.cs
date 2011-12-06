using Microsoft.VisualStudio.Text;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public class SnapshotLineRangeTest : EditorTestBase
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
        }

        [Test]
        public void Lines1()
        {
            Create("a", "b");
            var lineRange = SnapshotLineRange.CreateForLineAndMaxCount(_buffer.GetLine(0), 400);
            Assert.AreEqual(2, lineRange.Count);
        }
    }
}
