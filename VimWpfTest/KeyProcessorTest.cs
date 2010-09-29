using System;
using System.Windows;
using System.Windows.Input;
using Moq;
using NUnit.Framework;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyProcessorTest
    {
        private static RoutedEvent s_testEvent = EventManager.RegisterRoutedEvent(
                "Test Event",
                RoutingStrategy.Bubble,
                typeof(KeyProcessorTest),
                typeof(KeyProcessorTest));

        private IntPtr _keyboardId;
        private MockRepository _factory;
        private Mock<IVimBuffer> _buffer;
        private KeyProcessor _processor;

        [SetUp]
        public void Setup()
        {
        }

        public void Setup(string languageId)
        {
            if (!String.IsNullOrEmpty(languageId))
            {
                _keyboardId = NativeMethods.LoadKeyboardLayout(languageId, NativeMethods.KLF_ACTIVATE);
                Assert.AreNotEqual(_keyboardId, IntPtr.Zero);
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
            }
            _keyboardId = IntPtr.Zero;
        }

        private KeyEventArgs CreateKeyEventArgs(
            Key key,
            ModifierKeys modKeys = ModifierKeys.None)
        {
            var device = new MockKeyboardDevice(InputManager.Current) { ModifierKeysImpl = modKeys };
            var arg = new KeyEventArgs(
                device,
                new MockPresentationSource(),
                0,
                key);
            arg.RoutedEvent = s_testEvent;
            return arg;
        }

        [Test]
        [Description("Don't handle AltGR keys")]
        public void KeyDown1()
        {
            Setup(NativeMethods.LanguagePortuguese);
            var arg = CreateKeyEventArgs(Key.D8, ModifierKeys.Alt | ModifierKeys.Control);
            _processor.KeyDown(arg);
            Assert.IsFalse(arg.Handled);
        }

        [Test]
        [Description("Don't handle non-input keys")]
        public void KeyDown2()
        {
            foreach (var cur in new Key[] { Key.LeftAlt, Key.RightAlt, Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift })
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

            var array = new Key[] { Key.Enter, Key.Left, Key.Right, Key.Return };
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

            var array = new Key[] { Key.Enter, Key.Left, Key.Right, Key.Return };
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
        [Description("Do pass Control and Alt modified input onto the IVimBuffer")]
        public void KeyDown7()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true).Verifiable();
            _buffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(true).Verifiable();

            for (var i = 0; i < 26; i++)
            {
                var key = (Key)((int)Key.A + i);
                var arg = CreateKeyEventArgs(key, ModifierKeys.Alt);
                _processor.KeyDown(arg);
                Assert.IsTrue(arg.Handled);

                arg = CreateKeyEventArgs(key, ModifierKeys.Control);
                _processor.KeyDown(arg);
                Assert.IsTrue(arg.Handled);
            }

            _factory.Verify();
        }
    }
}
