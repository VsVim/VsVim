using System;
using System.Windows.Input;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.UnitTest
{
    public class VimKeyProcessorTest : IDisposable
    {
        protected IntPtr _keyboardId;
        protected bool _mustUnloadLayout;
        protected MockRepository _factory;
        protected Mock<IVimBuffer> _buffer;
        protected VimKeyProcessor _processor;

        public VimKeyProcessorTest()
        {
            Setup(null);
        }

        /// <summary>
        /// Setup the KeyProcessor giving it the keyboard layout specified by the provided
        /// language id
        /// </summary>
        protected virtual void Setup(string languageId)
        {
            if (!String.IsNullOrEmpty(languageId))
            {
                _keyboardId = NativeMethods.LoadKeyboardLayout(languageId, NativeMethods.KLF_ACTIVATE, out _mustUnloadLayout);
                Assert.NotEqual(_keyboardId, IntPtr.Zero);
            }
            else
            {
                _keyboardId = IntPtr.Zero;
            }

            _factory = new MockRepository(MockBehavior.Strict);
            _buffer = _factory.Create<IVimBuffer>();
            _processor = new VimKeyProcessor(_buffer.Object);
        }

        public void Dispose()
        {
            if (_keyboardId != IntPtr.Zero)
            {
                if (_mustUnloadLayout)
                {
                    Assert.True(NativeMethods.UnloadKeyboardLayout(_keyboardId));
                }

                NativeMethods.LoadKeyboardLayout(NativeMethods.LayoutEnglish, NativeMethods.KLF_ACTIVATE);
            }
            _keyboardId = IntPtr.Zero;
        }

        private static KeyEventArgs CreateKeyEventArgs(
            Key key,
            ModifierKeys modKeys = ModifierKeys.None)
        {
            var device = new MockKeyboardDevice(InputManager.Current) { ModifierKeysImpl = modKeys };
            return device.CreateKeyEventArgs(key, modKeys);
        }

        /// <summary>
        /// Don't handle AltGR keys
        /// </summary>
        [Fact]
        public void KeyDown1()
        {
            Setup(NativeMethods.LayoutPortuguese);
            var arg = CreateKeyEventArgs(Key.D8, ModifierKeys.Alt | ModifierKeys.Control);
            _processor.KeyDown(arg);
            Assert.False(arg.Handled);
        }

        /// <summary>
        /// Don't handle non-input keys
        /// </summary>
        [Fact]
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
        /// If alpha characters can be definitively mapped then handle them here
        /// </summary>
        [Fact]
        public void KeyDown_Alpha()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key);
                _processor.KeyDown(arg);
                Assert.True(arg.Handled);
            }
        }

        /// <summary>
        /// Do handle non printable characters here
        /// </summary>
        [Fact]
        public void KeyDown4()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

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
        [Fact]
        public void KeyDown5()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false).Verifiable();

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
        /// Do pass Control and Alt modified input onto the IVimBuffer
        /// </summary>
        [Fact]
        public void KeyDown6()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                _processor.KeyDown(arg);
                Assert.False(arg.Handled);

                arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                _processor.KeyDown(arg);
                Assert.False(arg.Handled);
            }

            _factory.Verify();
        }

        /// <summary>
        /// Control + char won't end up as TextInput so we handle it directly
        /// </summary>
        [Fact]
        public void KeyDown_PassControlLetterToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                _processor.KeyDown(arg);
                Assert.True(arg.Handled);
            }

            _factory.Verify();
        }

        /// <summary>
        /// Need to handle 'Alt+char' in KeyDown since it won't end up as TextInput.
        /// </summary>
        [Fact]
        public void KeyDown_PassAltLetterToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                _processor.KeyDown(arg);
                Assert.True(arg.Handled);
            }

            _factory.Verify();
        }

        [Fact]
        public void KeyDown_PassNonCharOnlyToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

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

        [Fact]
        public void KeyDown_NonCharWithModifierShouldCarryModifier()
        {
            var ki = KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Shift);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch)).Verifiable();

            var arg = CreateKeyEventArgs(Key.Left, ModifierKeys.Shift);
            _processor.KeyDown(arg);
            Assert.True(arg.Handled);
            _buffer.Verify();
        }

        /// <summary>
        /// The way in which we translate Shift + Escape makes it a candidate for the KeyDown 
        /// event.  It shouldn't be processed though in insert mode since it maps to a character
        /// and would rendere as invisible data if processed as an ITextBuffer edit
        /// </summary>
        [Fact]
        public void KeyDown_ShiftPlusEscape()
        {
            KeyInput ki;
            Assert.True(KeyUtil.TryConvertToKeyInput(Key.Escape, ModifierKeys.Shift, out ki));
            _buffer.Setup(x => x.CanProcess(ki)).Returns(false).Verifiable();

            var arg = CreateKeyEventArgs(Key.Escape, ModifierKeys.Shift);
            _processor.KeyDown(arg);
            Assert.False(arg.Handled);
            _buffer.Verify();
        }
    }
}
