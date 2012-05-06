using System;
using System.Threading;
using System.Windows.Threading;
using NUnit.Framework;
using Vim.UI.Wpf.Implementation;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    [TestFixture]
    public sealed class KeyMappingTimeoutHandlerTest : VimTestBase
    {
        private KeyMappingTimeoutHandler _keyMappingTimeoutHandler;
        private IVimBuffer _vimBuffer;

        public override void SetupBase()
        {
            base.SetupBase();
            _keyMappingTimeoutHandler = new KeyMappingTimeoutHandler(ProtectedOperations);
            Vim.GlobalSettings.Timeout = true;
            Vim.GlobalSettings.TimeoutLength = 100;

            _vimBuffer = CreateVimBuffer("");
            _keyMappingTimeoutHandler.OnVimBufferCreated(_vimBuffer);
        }

        /// <summary>
        /// Central place for waiting for the timer to expire.  The timer fires at Input priority so
        /// wait the timeout and queue at a lower priority event
        /// </summary>
        private void WaitForTimer()
        {
            var happened = false;
            var count = 0;
            EventHandler handler = delegate { happened = true; };
            _keyMappingTimeoutHandler.Tick += handler;

            while (!happened && count < 20)
            {
                Thread.Sleep(Vim.GlobalSettings.TimeoutLength);
                Dispatcher.CurrentDispatcher.DoEvents();
                count++;
            }

            _keyMappingTimeoutHandler.Tick -= handler;
            Assert.IsTrue(happened);
        }

        /// <summary>
        /// A timeout after a single key stroke should cause the keystroke to 
        /// be processed
        /// </summary>
        [Test]
        public void Timeout_Single()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.AreEqual("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.AreEqual("c", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// A timeout after a double key stroke should cause the buffered keystrokes to 
        /// be processed
        /// </summary>
        [Test]
        public void Timeout_Double()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.AreEqual("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('a');
            Assert.AreEqual("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.AreEqual("ca", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        [Test]
        public void NoTimeout()
        {
            _vimBuffer.Vim.GlobalSettings.TimeoutLength = 1000;
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            _vimBuffer.Process('a');
            Thread.Sleep(50);
            Assert.AreEqual("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('t');
            Assert.AreEqual("chase the cat", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }
    }
}
