using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf.Implementation.Mouse;
using Vim.UnitTest;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class VimMouseProcessorTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly Mock<IKeyboardDevice> _keyboardDevice;
        private readonly VimMouseProcessor _vimMouseProcessor;

        protected VimMouseProcessorTest()
        {
            _vimBuffer = CreateVimBuffer("");
            _keyboardDevice = new Mock<IKeyboardDevice>(MockBehavior.Loose);
            _keyboardDevice.SetupGet(x => x.KeyModifiers).Returns(VimKeyModifiers.None);
            _vimMouseProcessor = new VimMouseProcessor(_vimBuffer, _keyboardDevice.Object);
        }

        public sealed class TryProcessTest : VimMouseProcessorTest
        {
            /// <summary>
            /// Visual Mode doesn't actually process any mouse commands.  All of the selection tracking
            /// is handled elsewhere.  If the mouse commands end up making it to Visual Mode it will
            /// result in a beep event.  Verify that doesn't happen
            /// </summary>
            [Fact]
            public void VisualMode()
            {
                _vimBuffer.ProcessNotation("v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                foreach (var keyInput in KeyInputUtil.VimKeyInputList.Where(x => x.IsMouseKey))
                {
                    Assert.False(_vimMouseProcessor.TryProcess(keyInput.Key));
                    Assert.Equal(0, VimHost.BeepCount);
                }
            }

            [Fact]
            public void GoToDefinition()
            {
                _keyboardDevice.SetupGet(x => x.KeyModifiers).Returns(VimKeyModifiers.Control);
                VimHost.GoToDefinitionReturn = false;
                _vimMouseProcessor.TryProcess(VimKey.LeftMouse);
                Assert.Equal(1, VimHost.GoToDefinitionCount);
            }

            [Fact]
            public void Issue1317()
            {
                _vimBuffer.ProcessNotation("v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimMouseProcessor.TryProcess(VimKey.LeftDrag));
                Assert.Equal(0, VimHost.BeepCount);
            }
        }
    }
}
