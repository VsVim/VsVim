using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.EditorHost;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class VimKeyProcessorTest : VimTestBase
    {
        protected VimKeyProcessor _processor;

        public VimKeyProcessorTest()
        {
            _processor = CreateKeyProcessor();
        }

        protected static KeyEventArgs CreateKeyEventArgs(
            Key key,
            ModifierKeys modKeys = ModifierKeys.None)
        {
            var device = new MockKeyboardDevice(InputManager.Current) { ModifierKeysImpl = modKeys };
            return device.CreateKeyEventArgs(key, modKeys);
        }

        protected abstract VimKeyProcessor CreateKeyProcessor();

        public sealed class KeyDownTest : VimKeyProcessorTest
        {
            private MockRepository _factory;
            private Mock<IVimBuffer> _mockVimBuffer;

            protected override Wpf.VimKeyProcessor CreateKeyProcessor()
            {
                _factory = new MockRepository(MockBehavior.Strict);
                _mockVimBuffer = _factory.Create<IVimBuffer>();
                return new VimKeyProcessor(_mockVimBuffer.Object, KeyUtil);
            }

            private void AssertHandled(Key key, ModifierKeys modifierKeys)
            {
                var arg = CreateKeyEventArgs(key, modifierKeys);
                _processor.KeyDown(arg);
                Assert.True(arg.Handled);
            }

            private void AssertNotHandled(Key key, ModifierKeys modifierKeys)
            {
                var arg = CreateKeyEventArgs(key, modifierKeys);
                _processor.KeyDown(arg);
                Assert.False(arg.Handled);
            }

            /// <summary>
            /// Don't handle keys with AltGR levels
            /// </summary>
            [WpfFact]
            public void KeyDown1()
            {
                _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>()))
                    .Returns(true);
                _mockVimBuffer.Setup(x => x.Process(It.IsAny<KeyInput>()))
                    .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));

                // Temporarily switch to a US international keyboard
                // that has keys with AltGr levels.
                var oldKeyboardlayout = NativeMethods.GetKeyboardLayout(0);
                var usInternationalLayout = NativeMethods.LoadKeyboardLayout("00020409",
                    NativeMethods.KLF_ACTIVATE, out bool needUnload);
                try
                {
                    // <c> should be 'c'.
                    AssertNotHandled(Key.C, ModifierKeys.None);

                    // <S-c> should be 'C'.
                    AssertNotHandled(Key.C, ModifierKeys.Shift);

                    // <AltGr-c> should be '©'.
                    AssertNotHandled(Key.C, ModifierKeys.Control | ModifierKeys.Alt);

                    // <AltGr-S-c> should be '¢'.
                    AssertNotHandled(Key.C, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);

                    // <f> should be 'f'.
                    AssertNotHandled(Key.F, ModifierKeys.None);

                    // <S-f> should be 'F'.
                    AssertNotHandled(Key.F, ModifierKeys.Shift);

                    // <AltGr-f> can be <C-A-f>.
                    AssertHandled(Key.F, ModifierKeys.Control | ModifierKeys.Alt);

                    // <AltGr-S-f> can be <C-S-A-f>.
                    AssertHandled(Key.F, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);
                }
                finally
                {
                    NativeMethods.ActivateKeyboardLayout(oldKeyboardlayout, 0);
                    if (needUnload)
                    {
                        NativeMethods.UnloadKeyboardLayout(usInternationalLayout);
                    }
                }
            }

            /// <summary>
            /// Don't handle non-input keys
            /// </summary>
            [WpfFact]
            public void KeyDown2()
            {
                foreach (var cur in new[] { Key.LeftAlt, Key.RightAlt, Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift })
                {
                    var arg = CreateKeyEventArgs(cur);
                    _processor.KeyDown(arg);
                    Assert.False(arg.Handled);
                }
            }

            /// <summary>
            /// Don't handle any alpha characters in the KeyDown event.  Textual input should be 
            /// handled in TextInput not KeyDown
            /// </summary>
            [WpfFact]
            public void DontHandleAlpha()
            {
                for (var i = 0; i < 26; i++)
                {
                    var key = (Key)((int)Key.A + i);
                    var arg = CreateKeyEventArgs(key);
                    _processor.KeyDown(arg);
                    Assert.False(arg.Handled);
                }
            }

            /// <summary>
            /// Do handle non printable characters here
            /// </summary>
            [WpfFact]
            public void KeyDown4()
            {
                _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
                _mockVimBuffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

                var array = new[] { Key.Enter, Key.Left, Key.Right, Key.Return };
                foreach (var cur in array)
                {
                    var arg = CreateKeyEventArgs(cur);
                    _processor.KeyDown(arg);
                    Assert.True(arg.Handled);
                }

                _factory.Verify();
            }

            /// <summary>
            /// Do pass non-printable charcaters onto the IVimBuffer
            /// </summary>
            [WpfFact]
            public void KeyDown5()
            {
                _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false).Verifiable();

                var array = new[] { Key.Enter, Key.Left, Key.Right, Key.Return };
                foreach (var cur in array)
                {
                    var arg = CreateKeyEventArgs(cur);
                    _processor.KeyDown(arg);
                    Assert.False(arg.Handled);
                }

                _factory.Verify();
            }

            /// <summary>
            /// Control + char will end up as Control text and should be passed onto TextInput
            /// </summary>
            [WpfFact]
            public void PassControlLetterToBuffer()
            {
                for (var i = 0; i < 26; i++)
                {
                    var key = (Key)((int)Key.A + i);
                    var arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                    _processor.KeyDown(arg);
                    Assert.False(arg.Handled);
                }
            }

            /// <summary>
            /// The Alt key when combined with a char will be passed as TextComposition::System text and 
            /// we should hence handle it in the TextInput handler and not KeyDown
            /// </summary>
            [WpfFact]
            public void DontPassAltLetterToBuffer()
            {
                for (var i = 0; i < 26; i++)
                {
                    var key = (Key)((int)Key.A + i);
                    var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                    _processor.KeyDown(arg);
                    Assert.False(arg.Handled);
                }

                _factory.Verify();
            }

            [WpfFact]
            public void PassNonCharOnlyToBuffer()
            {
                _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
                _mockVimBuffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

                var array = new[] { Key.Left, Key.Right, Key.Up, Key.Down };
                foreach (var key in array)
                {
                    var modifiers = new[] { ModifierKeys.Shift, ModifierKeys.Alt, ModifierKeys.Control, ModifierKeys.None };
                    foreach (var mod in modifiers)
                    {
                        var arg = CreateKeyEventArgs(key, mod);
                        _processor.KeyDown(arg);
                        Assert.True(arg.Handled);
                    }
                }
            }

            [WpfFact]
            public void NonCharWithModifierShouldCarryModifier()
            {
                var ki = KeyInputUtil.ApplyKeyModifiersToKey(VimKey.Left, VimKeyModifiers.Shift);
                _mockVimBuffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
                _mockVimBuffer.Setup(x => x.Process(ki)).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

                var arg = CreateKeyEventArgs(Key.Left, ModifierKeys.Shift);
                _processor.KeyDown(arg);
                Assert.True(arg.Handled);
                _mockVimBuffer.Verify();
            }

            /// <summary>
            /// The way in which we translate Shift + Escape makes it a candidate for the KeyDown 
            /// event.  It shouldn't be processed though in insert mode since it maps to a character
            /// and would rendere as invisible data if processed as an ITextBuffer edit
            /// </summary>
            [WpfFact]
            public void ShiftPlusEscape()
            {
                Assert.True(KeyUtil.TryConvertSpecialToKeyInput(Key.Escape, ModifierKeys.Shift, out KeyInput ki));
                _mockVimBuffer.Setup(x => x.CanProcess(ki)).Returns(false).Verifiable();

                var arg = CreateKeyEventArgs(Key.Escape, ModifierKeys.Shift);
                _processor.KeyDown(arg);
                Assert.False(arg.Handled);
                _mockVimBuffer.Verify();
            }
        }

        public sealed class TextInputTest : VimKeyProcessorTest
        {
            private MockRepository _factory;
            private Mock<IVimBuffer> _mockVimBuffer;
            private IWpfTextView _wpfTextView;
            private readonly InputDevice _inputDevice = new KeyboardInputSimulation.DefaultKeyboardDevice();

            protected override VimKeyProcessor CreateKeyProcessor()
            {
                _factory = new MockRepository(MockBehavior.Strict);
                _mockVimBuffer = _factory.Create<IVimBuffer>();
                _wpfTextView = CreateTextView();
                return new VimKeyProcessor(_mockVimBuffer.Object, KeyUtil);
            }

            private TextCompositionEventArgs CreateTextComposition(string text)
            {
                return _wpfTextView.VisualElement.CreateTextCompositionEventArgs(text, _inputDevice);
            }

            [WpfFact]
            public void SimpleSystemText()
            {
                var keyInput = KeyInputUtil.CharToKeyInput('\u00C1');
                _mockVimBuffer.Setup(x => x.CanProcess(keyInput)).Returns(false);

                var args = CreateTextComposition("\u00C1");
                Assert.Equal("\u00C1", args.SystemText);
                _processor.TextInput(args);
                Assert.False(args.Handled);
                _mockVimBuffer.Verify();
            }
        }
    }
}
