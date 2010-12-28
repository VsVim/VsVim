using System;
using System.Windows.Input;
using Moq;
using NUnit.Framework;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyProcessorTest
    {
        protected IntPtr _keyboardId;
        protected MockRepository _factory;
        protected Mock<IVimBuffer> _buffer;
        protected KeyProcessor _processor;

        [SetUp]
        public void Setup()
        {
            Setup(null);
        }

        protected virtual void Setup(string languageId)
        {
            if (!String.IsNullOrEmpty(languageId))
            {
                _keyboardId = NativeMethods.LoadKeyboardLayout(languageId, NativeMethods.KLF_ACTIVATE);
                Assert.AreNotEqual(_keyboardId, IntPtr.Zero);
            }
            else
            {
                _keyboardId = IntPtr.Zero;
            }

            _factory = new MockRepository(MockBehavior.Strict);
            _buffer = _factory.Create<IVimBuffer>();
            _processor = new KeyProcessor(_buffer.Object);
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboardId != IntPtr.Zero)
            {
                Assert.IsTrue(NativeMethods.UnloadKeyboardLayout(_keyboardId));
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

        [Test]
        [Description("Don't handle AltGR keys")]
        public void KeyDown1()
        {
            Setup(NativeMethods.LayoutPortuguese);
            var arg = CreateKeyEventArgs(Key.D8, ModifierKeys.Alt | ModifierKeys.Control);
            _processor.KeyDown(arg);
            Assert.IsFalse(arg.Handled);
        }

        [Test]
        [Description("Don't handle non-input keys")]
        public void KeyDown2()
        {
            foreach (var cur in new[] { Key.LeftAlt, Key.RightAlt, Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift })
            {
                var arg = CreateKeyEventArgs(cur);
                _processor.KeyDown(arg);
                Assert.IsFalse(arg.Handled);
            }
        }

        [Test]
        [Description("Don't handle raw characters here.  Should be done in TextInput")]
        public void KeyDown3()
        {
            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key);
                _processor.KeyDown(arg);
                Assert.IsFalse(arg.Handled);
            }
        }

        [Test]
        [Description("Do handle non printable characters here")]
        public void KeyDown4()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(true).Verifiable();

            var array = new[] { Key.Enter, Key.Left, Key.Right, Key.Return };
            foreach (var cur in array)
            {
                var arg = CreateKeyEventArgs(cur);
                _processor.KeyDown(arg);
                Assert.IsTrue(arg.Handled);
            }

            _factory.Verify();
        }

        [Test]
        [Description("Do pass non-printable charcaters onto the IVimBuffer")]
        public void KeyDown5()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false).Verifiable();

            var array = new[] { Key.Enter, Key.Left, Key.Right, Key.Return };
            foreach (var cur in array)
            {
                var arg = CreateKeyEventArgs(cur);
                _processor.KeyDown(arg);
                Assert.IsFalse(arg.Handled);
            }

            _factory.Verify();
        }

        [Test]
        [Description("Do pass Control and Alt modified input onto the IVimBuffer")]
        public void KeyDown6()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                _processor.KeyDown(arg);
                Assert.IsFalse(arg.Handled);

                arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                _processor.KeyDown(arg);
                Assert.IsFalse(arg.Handled);
            }

            _factory.Verify();
        }

        [Test]
        [Description("Control + char won't end up as TextInput so we handle it directly")]
        public void KeyDown_PassControlLetterToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(true).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                _processor.KeyDown(arg);
                Assert.IsTrue(arg.Handled);
            }

            _factory.Verify();
        }

        [Test]
        [Description("Alt + char won't end up as TextInput so we handle it directly")]
        public void KeyDown_PassAltLetterToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(true).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                _processor.KeyDown(arg);
                Assert.IsTrue(arg.Handled);
            }

            _factory.Verify();
        }

        [Test]
        public void KeyDown_PassNonCharOnlyToBuffer()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(true).Verifiable();

            var array = new[] { Key.Left, Key.Right, Key.Up, Key.Down };
            foreach (var key in array)
            {
                var modifiers = new[] { ModifierKeys.Shift, ModifierKeys.Alt, ModifierKeys.Control, ModifierKeys.None };
                foreach (var mod in modifiers)
                {
                    var arg = CreateKeyEventArgs(key, mod);
                    _processor.KeyDown(arg);
                    Assert.IsTrue(arg.Handled);
                }
            }
        }

        [Test]
        public void KeyDown_NonCharWithModifierShouldCarryModifier()
        {
            var ki = KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.Left, KeyModifiers.Shift);
            _buffer.Setup(x => x.CanProcess(ki)).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(ki)).Returns(true).Verifiable();

            var arg = CreateKeyEventArgs(Key.Left, ModifierKeys.Shift);
            _processor.KeyDown(arg);
            Assert.IsTrue(arg.Handled);
            _buffer.Verify();
        }
    }
}
