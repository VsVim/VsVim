using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class SnapshotLineSpanTest
    {
        private ITextBuffer _buffer;

        public void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
        }

        [Test]
        public void Lines1()
        {
            Create("a", "b");
            var lineSpan = SnapshotLineSpanUtil.CreateForStartAndEndLine(_buffer.CurrentSnapshot, 0, 1);
            Assert.AreEqual(1, lineSpan.Lines.First().LineNumber);
            Assert.AreEqual(1, lineSpan.Lines.Last().LineNumber);
        }
    }
}
