using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MotionUtilLegacyTest
    {
        private ITextBuffer _textBuffer;
        private ITextSnapshot _snapshot;

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _textBuffer.CurrentSnapshot;
        }

    }
}
