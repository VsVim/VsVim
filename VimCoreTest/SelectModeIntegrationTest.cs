using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using EditorUtils;

namespace Vim.UnitTest
{
    public abstract class SelectModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected ITextSelection _textSelection;
        protected IVimGlobalSettings _globalSettings;
        protected TestableSynchronizationContext _context;

        protected void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textBuffer = _vimBuffer.TextBuffer;
            _globalSettings = _vimBuffer.GlobalSettings;
            _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
            _textSelection = _textView.Selection;
            _context = new TestableSynchronizationContext();
            _context.Install();
        }

        protected void EnterSelect(int start, int length)
        {
            var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, start, length);
            _textView.SelectAndMoveCaret(span);
            _context.RunAll();
            Assert.AreEqual(ModeKind.Select, _vimBuffer.ModeKind);
        }

        [TestFixture]
        public sealed class Enter : SelectModeIntegrationTest
        {
            [Test]
            public void SelectOfText()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.AreEqual(ModeKind.Select, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Extending the selection should just ask select mode to reset it's information
            /// </summary>
            [Test]
            public void ExtendSelection()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.AreEqual(ModeKind.Select, _vimBuffer.ModeKind);
                _textSelection.Select(0, 5);
                Assert.IsFalse(_context.IsEmpty);
                _context.RunAll();
                Assert.AreEqual(ModeKind.Select, _vimBuffer.ModeKind);
            }
        }

        [TestFixture]
        public sealed class Edit : SelectModeIntegrationTest
        {
            [Test]
            public void SimpleAlpha()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('o');
                Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.AreEqual("To time", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            }

            [Test]
            public void SimpleNumeric()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('3');
                Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.AreEqual("T3 time", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            }

            [Test]
            public void SimpleEnter()
            {
                Create("dog go time");
                EnterSelect(4, 3);
                _vimBuffer.Process(VimKey.Enter);
                Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
                CollectionAssert.AreEqual(new[] { "dog ", "time" }, _textBuffer.GetLines());
                Assert.AreEqual(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }
        }

        [TestFixture]
        public sealed class Misc : SelectModeIntegrationTest
        {
            /// <summary>
            /// Make sure that escape will leave select mode
            /// </summary>
            [Test]
            public void Escape()
            {
                Create("cat dog");
                EnterSelect(0, 3);
                _vimBuffer.Process(VimKey.Escape);
                Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            }
        }
    }
}
