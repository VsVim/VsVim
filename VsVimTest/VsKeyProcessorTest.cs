using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class VsKeyProcessorTest : Vim.UI.Wpf.Test.KeyProcessorTest
    {
        private Mock<IVsAdapter> _adapter;
        private Mock<ITextBuffer> _textBuffer;
        private VsKeyProcessor _vsProcessor;
        private MockKeyboardDevice _device;

        protected override void Setup(string languageId)
        {
            base.Setup(languageId);
            _factory = new MockRepository(MockBehavior.Strict);
            _textBuffer = _factory.Create<ITextBuffer>();
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(false);
            _adapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
            _buffer = MockObjectFactory.CreateVimBuffer(_textBuffer.Object);
            _vsProcessor = new VsKeyProcessor(_adapter.Object, _buffer.Object);
            _processor = _vsProcessor;
            _device = new MockKeyboardDevice();
        }

        private void VerifyHandle(Key key, ModifierKeys modKeys = ModifierKeys.None)
        {
            VerifyCore(key, modKeys, shouldHandle: true);
        }

        private void VerifyNotHandle(Key key, ModifierKeys modKeys = ModifierKeys.None)
        {
            VerifyCore(key, modKeys, shouldHandle: false);
        }

        private void VerifyCore(Key key, ModifierKeys modKeys, bool shouldHandle)
        {
            var args = _device.CreateKeyEventArgs(key, modKeys);
            _vsProcessor.KeyDown(args);
            Assert.AreEqual(shouldHandle, args.Handled, "Did not handle {0} + {1}", key, modKeys);
        }

        [Test]
        public void EditableKeyDown1()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(false).Verifiable();
            VerifyNotHandle(Key.A);
            _factory.Verify();
        }

        [Test]
        public void EditableKeyDown2()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(false).Verifiable();
            VerifyNotHandle(Key.D);
            _factory.Verify();
        }

        [Test]
        public void EditableKeyDown3()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(false).Verifiable();
            VerifyNotHandle(Key.D, ModifierKeys.Alt | ModifierKeys.Control);
            _factory.Verify();
        }

        [Test]
        [Description("Handle input when it's readonly")]
        public void ReadOnlyKeyDown1()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(true).Verifiable();
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();
            VerifyHandle(Key.A);
            VerifyHandle(Key.B);
            VerifyHandle(Key.D1);
            VerifyHandle(Key.A, ModifierKeys.Shift);
            VerifyHandle(Key.B, ModifierKeys.Shift);
            VerifyHandle(Key.D1, ModifierKeys.Shift);
            _factory.Verify();
        }

        [Test]
        [Description("Don't handle non-input when the buffer is readonly")]
        public void ReadOnlyKeyDown2()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(true).Verifiable();
            VerifyNotHandle(Key.LeftCtrl);
            VerifyNotHandle(Key.RightCtrl);
            VerifyNotHandle(Key.LeftAlt);
            VerifyNotHandle(Key.RightAlt);
            VerifyNotHandle(Key.LeftShift);
            VerifyNotHandle(Key.RightShift);
        }

        [Test]
        [Description("Handle all characters in read only")]
        public void ReadOnlyKeyDown3()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(true).Verifiable();
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();
            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                VerifyHandle(key);
            }
        }

        [Test]
        [Description("Don't handle AltGr combos")]
        public void ReadOnlyKeyDown4()
        {
            _adapter.Setup(x => x.IsReadOnly(_textBuffer.Object)).Returns(true).Verifiable();
            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                VerifyNotHandle(key, ModifierKeys.Control | ModifierKeys.Alt);
            }
        }

        [Test]
        public void KeyDown_DontHandleIfIncrementalSearchActive()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.CanProcessAsCommand(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();
            VerifyHandle(Key.Enter);
            _adapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
            VerifyNotHandle(Key.Enter);
        }
    }
}
