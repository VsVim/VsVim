using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public class SnapshotUtilTest : VimTestBase
    {
        private static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        private ITextBuffer _buffer = null;
        private ITextSnapshot _snapshot = null;

        internal void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [WpfFact]
        public void GetStartPoint()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.Equal(line.Start, start);
        }

        [WpfFact]
        public void GetEndPoint()
        {
            Create("foo bar");
            var end = SnapshotUtil.GetEndPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.Equal(line.End, end);
        }
    }
}
