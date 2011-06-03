using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    class BlockCaretControllerTest
    {
        private MockRepository _factory;
        private Mock<ITextView> _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<IBlockCaret> _caret;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<IVimLocalSettings> _localSettings;
        private BlockCaretController _controller;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _textView = _factory.Create<ITextView>(MockBehavior.Strict);
            _settings = _factory.Create<IVimGlobalSettings>(MockBehavior.Loose);
            _localSettings = MockObjectFactory.CreateLocalSettings(global: _settings.Object, factory: _factory);
            _buffer = _factory.Create<IVimBuffer>(MockBehavior.Strict);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            _buffer.SetupGet(x => x.TextView).Returns(_textView.Object);
            _buffer.SetupGet(x => x.LocalSettings).Returns(_localSettings.Object);
            _caret = _factory.Create<IBlockCaret>(MockBehavior.Strict);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Invisible);
            _caret.SetupSet(x => x.CaretOpacity = It.IsAny<double>());
            _controller = new BlockCaretController(_buffer.Object, _caret.Object);
        }

        [Test]
        public void OperatorPending1()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.OperatorPending).Verifiable();
            _buffer.SetupGet(x => x.NormalMode).Returns(mode.Object);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);

            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.HalfBlock).Verifiable();
            _controller.Update();
            _caret.Verify();
        }


        [Test, Description("Other modes shouldn't even consider operator pending")]
        public void OperatorPending2()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.OperatorPending).Verifiable();
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
            mode.SetupGet(x => x.KeyRemapMode).Returns(KeyRemapMode.Normal).Verifiable();
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
            _buffer.SetupGet(x => x.IncrementalSearch).Returns(search.Object);

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
            _buffer.SetupGet(x => x.IncrementalSearch).Returns(search.Object);
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
            _settings.SetupGet(x => x.IsSelectionInclusive).Returns(true);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualBlock);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block);
            _controller.Update();
        }

        [Test]
        public void VisualMode2()
        {
            _settings.SetupGet(x => x.IsSelectionInclusive).Returns(true);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter);
            _caret.SetupSet(x => x.CaretDisplay = CaretDisplay.Block);
            _controller.Update();
        }

        [Test]
        public void VisualMode3()
        {
            _settings.SetupGet(x => x.IsSelectionInclusive).Returns(true);
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

        [Test]
        public void CaretOpacity1()
        {
            _caret.SetupSet(x => x.CaretOpacity = 0.01d).Verifiable();
            var setting = new Setting(
                GlobalSettingNames.CaretOpacityName,
                "",
                SettingKind.StringKind,
                SettingValue.NewStringValue(""),
                SettingValue.NewStringValue(""),
                true);
            _settings.SetupGet(x => x.CaretOpacity).Returns(1);
            _settings.Raise(x => x.SettingChanged += null, null, setting);
            _caret.Verify();
        }
    }
}
