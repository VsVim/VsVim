using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SnapshotLineRangeTest
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateTextBuffer(lines);
        }

        [Test]
        public void Lines1()
        {
            Create("a", "b");
            var lineSpan = SnapshotLineRangeUtil.CreateForLineNumberAndCount(_buffer.CurrentSnapshot, 0, 1).Value;
            Assert.AreEqual(0, lineSpan.Lines.First().LineNumber);
            Assert.AreEqual(0, lineSpan.Lines.Last().LineNumber);
        }
    }
}
