using System.Threading;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class SelectionChangeTrackerTest
    {
        private MockFactory _factory;
        private MockVimBuffer _buffer;
        private ITextView _textView;
        private TestableSynchronizationContext _context;

        [SetUp]
        public void Setup()
        {
            _textView = EditorUtil.CreateView("dog", "cat", "chicken", "pig");
            _factory = new MockFactory(MockBehavior.Strict);
            _buffer = new MockVimBuffer() { TextViewImpl = _textView, TextBufferImpl = _textView.TextBuffer };
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [Test]
        [Description("Already in Visual Mode.  Nothnig to do")]
        public void SelectionChanged1()
        {
            _buffer.IsProcessingInputImpl = false;
            _buffer.ModeKindImpl = ModeKind.VisualBlock;
            _textView.Selection.Select(_textView.GetLineSpan(0), false);
            Assert.IsTrue(_context.IsEmpty);
        }

        [Test]
        public void EventTest()
        {
            var didSee = false;
            var buffer = new Mock<IVimBuffer>(MockBehavior.Loose);
            buffer.Object.KeyInputBuffered += delegate
            {
                didSee = true;
            };
            buffer.Raise(x => x.KeyInputBuffered += null, null, KeyInputUtil.CharToKeyInput('c'));
            Assert.IsTrue(didSee);
        }

    }
}
