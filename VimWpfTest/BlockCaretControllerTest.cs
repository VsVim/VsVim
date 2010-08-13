using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    class BlockCaretControllerTest
    {
        private Mock<ITextView> _view;
        private Mock<IVimBuffer> _buffer;
        private Mock<IBlockCaret> _caret;
        private BlockCaretController _controller;

        [SetUp]
        public void SetUp()
        {
            _view = new Mock<ITextView>();
            _buffer = new Mock<IVimBuffer>();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            _buffer.SetupGet(x => x.TextView).Returns(_view.Object);
            _caret = new Mock<IBlockCaret>(MockBehavior.Strict);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Invisible);
            _controller = new BlockCaretController(_buffer.Object, _caret.Object);
        }

        [Test]
        public void OperatorPending1()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.IsOperatorPending).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.HalfBlock).Verifiable();
            _controller.Update();
            _caret.Verify();
        }


        [Test, Description("Other modes shouldn't even consider operator pending")]
        public void OperaterPending2()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.IsOperatorPending).Returns(true).Verifiable();
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Invisible).Verifiable();
            _controller.Update();
            _caret.Verify();
        }

        [Test]
        public void IsInReplace1()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.IsInReplace).Returns(true);
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.QuarterBlock).Verifiable();
            _controller.Update();
            _caret.Verify();
        }

        [Test, Description("Replace wins over operator pending")]
        public void IsInReplace2()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.IsInReplace).Returns(true);
            mode.SetupGet(x => x.IsOperatorPending).Returns(true);
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.QuarterBlock).Verifiable();
            _controller.Update();
            _caret.Verify();
        }

        [Test]
        public void NormalMode1()
        {
            var mode = new Mock<INormalMode>();
            var search = new Mock<IIncrementalSearch>();
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            mode.SetupGet(x => x.IncrementalSearch).Returns(search.Object);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block).Verifiable();
            _controller.Update();
            _caret.Verify();
        }

        [Test]
        public void NormalMode2()
        {
            var mode = new Mock<INormalMode>();
            var search = new Mock<IIncrementalSearch>();
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            mode.SetupGet(x => x.IncrementalSearch).Returns(search.Object);
            search.SetupGet(x => x.InSearch).Returns(true);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Invisible).Verifiable();
            _controller.Update();
            _caret.Verify();
        }

        [Test]
        public void CommandMode1()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Invisible);
            _controller.Update();
        }

        [Test]
        public void DisabledMode1()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Disabled);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.NormalCaret);
            _controller.Update();
        }

        [Test]
        public void VisualMode1()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualBlock);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block);
            _controller.Update();
        }

        [Test]
        public void VisualMode2()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block);
            _controller.Update();
        }

        [Test]
        public void VisualMode3()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualLine);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block);
            _controller.Update();
        }

        [Test]
        public void ReplaceMode1()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Replace);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.QuarterBlock);
            _controller.Update();
        }
    }
}
