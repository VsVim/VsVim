using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Summary description for TssUtilTest
    /// </summary>
    [TestFixture]
    public class TssUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _textBuffer = null;
        ITextSnapshot _snapshot = null;

        [SetUp]
        public void Init()
        {
            Create(s_lines);
        }

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _textBuffer.CurrentSnapshot;
        }

        [Test, Description("End of line should not have a current word")]
        public void FindCurrentWordSpan1()
        {
            Create("foo bar");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var opt = TssUtil.FindCurrentWordSpan(line.End, WordKind.NormalWord);
            Assert.IsTrue(opt.IsNone());
        }
    }
}
