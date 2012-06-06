using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim;
using Vim.UI.Wpf.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.Implementation;

namespace VsVim.UnitTest
{
    public sealed class VsKeyProcessorTest : VimKeyProcessorTest
    {
        private Mock<IVsAdapter> _vsAdapter;
        private Mock<ITextBuffer> _textBuffer;
        private IVimBufferCoordinator _bufferCoordinator;
        private VsKeyProcessor _vsProcessor;
        private MockKeyboardDevice _device;

        protected override void Setup(string languageId)
        {
            base.Setup(languageId);
            _factory = new MockRepository(MockBehavior.Strict);
            _textBuffer = _factory.Create<ITextBuffer>();
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
            _buffer = MockObjectFactory.CreateVimBuffer(_textBuffer.Object);
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _bufferCoordinator = new VimBufferCoordinator(_buffer.Object);
            _vsProcessor = new VsKeyProcessor(_vsAdapter.Object, _bufferCoordinator);
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
            Assert.Equal(shouldHandle, args.Handled);
        }

        /// <summary>
        /// Don't handle the AltGr scenarios here.  The AltGr key is just too ambiguous to handle in the 
        /// KeyDown event
        /// </summary>
        [Fact]
        public void KeyDown_AltGr()
        {
            VerifyNotHandle(Key.D, ModifierKeys.Alt | ModifierKeys.Control);
        }

        /// <summary>
        /// Make sure that we handle alpha input when the buffer is marked as readonly. 
        /// </summary>
        [Fact]
        public void KeyDown_AlphaReadOnly()
        {
            VerifyHandle(Key.A);
            VerifyHandle(Key.B);
            VerifyHandle(Key.D1);
            VerifyHandle(Key.A, ModifierKeys.Shift);
            VerifyHandle(Key.B, ModifierKeys.Shift);
            VerifyHandle(Key.D1, ModifierKeys.Shift);
        }

        /// <summary>
        /// If incremental search is active then we don't want to route input to VsVim.  Instead we 
        /// want to let it get processed by incremental search
        /// </summary>
        [Fact]
        public void KeyDown_DontHandleIfIncrementalSearchActive()
        {
            var all = new[] { Key.Enter, Key.A };
            foreach (var key in all)
            {
                _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
                VerifyHandle(key);
                _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                VerifyNotHandle(key);
            }
        }

        /// <summary>
        /// When presented with a KeyInput the TryProcess command should consider if the mapped key
        /// is a direct insert not the provided key.  
        /// </summary>
        [Fact]
        public void KeyDown_InsertCheckShouldConsiderMapped()
        {
            var keyInput = KeyInputUtil.CharWithControlToKeyInput('e');
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _buffer.Setup(x => x.CanProcessAsCommand(keyInput)).Returns(true).Verifiable();
            VerifyHandle(Key.E, ModifierKeys.Control);
            _factory.Verify();
        }

        /// <summary>
        /// We only do the CanProcessAsCommand check in insert mode.  The justification is that direct
        /// insert commands should go through IOleCommandTarget in order to trigger intellisense and
        /// the like.  If we're not in insert mode we don't consider intellisense in the key 
        /// processor
        /// </summary>
        [Fact]
        public void KeyDown_NonInsertShouldntCheckForCommand()
        {
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            VerifyHandle(Key.E, ModifierKeys.Control);
            _factory.Verify();
        }
    }
}
