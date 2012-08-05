using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim;
using Vim.UI.Wpf;
using Vim.UI.Wpf.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.Implementation.Misc;
using Xunit;
using Vim.UnitTest;

namespace VsVim.UnitTest
{
    public abstract class VsKeyProcessorTest : VimKeyProcessorTest
    {
        protected MockRepository _factory;
        protected Mock<IVimBuffer> _mockVimBuffer;
        protected Mock<IVsAdapter> _vsAdapter;
        protected Mock<ITextBuffer> _textBuffer;
        internal IVimBufferCoordinator _bufferCoordinator;
        protected MockKeyboardDevice _device;

        protected override VimKeyProcessor CreateKeyProcessor()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _textBuffer = _factory.Create<ITextBuffer>();
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
            _mockVimBuffer = MockObjectFactory.CreateVimBuffer(_textBuffer.Object);
            _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            _mockVimBuffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
            _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _bufferCoordinator = new VimBufferCoordinator(_mockVimBuffer.Object);
            _device = new MockKeyboardDevice();
            return new VsKeyProcessor(_vsAdapter.Object, _bufferCoordinator, KeyUtil);
        }

        public sealed class VsKeyDownTest : VsKeyProcessorTest
        {
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
                _processor.KeyDown(args);
                Assert.Equal(shouldHandle, args.Handled);
            }

            /// <summary>
            /// Don't handle the AltGr scenarios here.  The AltGr key is just too ambiguous to handle in the 
            /// KeyDown event
            /// </summary>
            [Fact]
            public void AltGr()
            {
                VerifyNotHandle(Key.D, ModifierKeys.Alt | ModifierKeys.Control);
            }

            /// <summary>
            /// Don't handle any alpha input in the KeyDown phase.  This should all be handled inside
            /// of the TextInput phase instead
            /// </summary>
            [Fact]
            public void AlphaKeys()
            {
                VerifyNotHandle(Key.A);
                VerifyNotHandle(Key.B);
                VerifyNotHandle(Key.D1);
                VerifyNotHandle(Key.A, ModifierKeys.Shift);
                VerifyNotHandle(Key.B, ModifierKeys.Shift);
                VerifyNotHandle(Key.D1, ModifierKeys.Shift);
            }

            /// <summary>
            /// If incremental search is active then we don't want to route input to VsVim.  Instead we 
            /// want to let it get processed by incremental search
            /// </summary>
            [Fact]
            public void DontHandleIfIncrementalSearchActive()
            {
                var all = new [] { Key.Enter, Key.Tab, Key.Back };
                foreach (var key in all)
                {
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
                    VerifyHandle(key);
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                    VerifyNotHandle(key);
                }
            }
        }

        public sealed class VsTextInputTest : VsKeyProcessorTest
        {
            private IWpfTextView _wpfTextView;

            public VsTextInputTest()
            {
                _wpfTextView = CreateTextView();
            }

            private TextCompositionEventArgs CreateTextComposition(string text)
            {
                return _wpfTextView.VisualElement.CreateTextCompositionEventArgs(text, _device);
            }

            private void VerifyHandle(string text)
            {
                VerifyCore(text, shouldHandle: true);
            }

            private void VerifyNotHandle(string text)
            {
                VerifyCore(text, shouldHandle: false);
            }

            private void VerifyCore(string text, bool shouldHandle)
            {
                var args = CreateTextComposition(text);
                _processor.TextInput(args);
                Assert.Equal(shouldHandle, args.Handled);
            }

            /// <summary>
            /// Make sure that alpha input is handled in TextInput
            /// </summary>
            [Fact]
            public void AlphaKeys()
            {
                var all = "ab1AB!";
                foreach (var current in all)
                {
                    VerifyHandle(current.ToString());
                }
            }

            /// <summary>
            /// If incremental search is active then we don't want to route input to VsVim.  Instead we 
            /// want to let it get processed by incremental search
            /// </summary>
            [Fact]
            public void DontHandleIfIncrementalSearchActive()
            {
                var all = new [] { KeyInputUtil.EnterKey, KeyInputUtil.CharToKeyInput('a') };
                foreach (var keyInput in all)
                {
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
                    VerifyHandle(keyInput.Char.ToString());
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                    VerifyNotHandle(keyInput.Char.ToString());
                }
            }

            /// <summary>
            /// When presented with a KeyInput the TryProcess command should consider if the mapped key
            /// is a direct insert not the provided key.  
            /// </summary>
            [Fact]
            public void InsertCheckShouldConsiderMapped()
            {
                var keyInput = KeyInputUtil.CharWithControlToKeyInput('e');
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _mockVimBuffer.Setup(x => x.CanProcessAsCommand(keyInput)).Returns(true).Verifiable();
                VerifyHandle(keyInput.Char.ToString());
                _factory.Verify();
            }

            /// <summary>
            /// We only do the CanProcessAsCommand check in insert mode.  The justification is that direct
            /// insert commands should go through IOleCommandTarget in order to trigger intellisense and
            /// the like.  If we're not in insert mode we don't consider intellisense in the key 
            /// processor
            /// </summary>
            [Fact]
            public void NonInsertShouldntCheckForCommand()
            {
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
                VerifyHandle(KeyInputUtil.CharWithControlToKeyInput('e').Char.ToString());
                _factory.Verify();
            }
        }
    }
}
