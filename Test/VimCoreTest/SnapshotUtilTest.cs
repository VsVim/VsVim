using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public class SnapshotUtilTest : VimTestBase
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        public void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Fact]
        public void GetStartPoint()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.Equal(line.Start, start);
        }

        [Fact]
        public void GetEndPoint()
        {
            Create("foo bar");
            var end = SnapshotUtil.GetEndPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.Equal(line.End, end);
        }
    }
}
