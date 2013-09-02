using System;
using System.Threading;
using System.Windows.Threading;
using Xunit;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    public sealed class KeyMappingTimeoutHandlerTest : VimTestBase
    {
        private readonly KeyMappingTimeoutHandler _keyMappingTimeoutHandler;
        private readonly IVimBuffer _vimBuffer;

        public KeyMappingTimeoutHandlerTest()
        {
            _keyMappingTimeoutHandler = new KeyMappingTimeoutHandler(VimProtectedOperations);
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
            Assert.True(happened);
        }

        /// <summary>
        /// A timeout after a single key stroke should cause the keystroke to 
        /// be processed
        /// </summary>
        [Fact]
        public void Timeout_Single()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.Equal("c", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// A timeout after a double key stroke should cause the buffered keystrokes to 
        /// be processed
        /// </summary>
        [Fact]
        public void Timeout_Double()
        {
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('a');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.Equal("ca", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        [Fact]
        public void NoTimeout()
        {
            _vimBuffer.Vim.GlobalSettings.TimeoutLength = 1000;
            _vimBuffer.Vim.KeyMap.MapWithNoRemap("cat", "chase the cat", KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            _vimBuffer.Process('a');
            Thread.Sleep(50);
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('t');
            Assert.Equal("chase the cat", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }
    }
}
