using Microsoft.VisualStudio.Text.Editor;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf.Implementation.Mouse;
using Vim.UnitTest;
using Vim.UnitTest.Exports;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class VimMouseProcessorTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly Mock<IKeyboardDevice> _keyboardDevice;
        private readonly VimMouseProcessor _vimMouseProcessor;
        private readonly ITextView _textView;
        private readonly TestableMouseDevice _testableMouseDevice;

        protected VimMouseProcessorTest()
        {
            _vimBuffer = CreateVimBuffer("cat", "");
            _keyboardDevice = new Mock<IKeyboardDevice>(MockBehavior.Loose);
            _keyboardDevice.SetupGet(x => x.KeyModifiers).Returns(VimKeyModifiers.None);
            _vimMouseProcessor = new VimMouseProcessor(_vimBuffer, _keyboardDevice.Object, ProtectedOperations);
            _textView = _vimBuffer.TextView;
            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            base.Dispose();
        }

        public sealed class TryProcessTest : VimMouseProcessorTest
        {
            [WpfFact]
            public void GoToDefinition()
            {
                SetVs2017AndAboveEditorOptionValue(_textView.Options, EditorOptionsUtil.ClickGoToDefOpensPeekId, false);
                _keyboardDevice.SetupGet(x => x.KeyModifiers).Returns(VimKeyModifiers.Control);
                var point = _textView.GetPointInLine(0, 0);
                _testableMouseDevice.Point = point;
                VimHost.GoToDefinitionReturn = false;
                _vimMouseProcessor.TryProcess(VimKey.LeftMouse);
                Assert.Equal(1, VimHost.GoToDefinitionCount);
                Assert.Equal(0, VimHost.PeekDefinitionCount);
            }

            [Vs2017AndAboveWpfFact]
            public void PeekDefinition()
            {
                SetVs2017AndAboveEditorOptionValue(_textView.Options, EditorOptionsUtil.ClickGoToDefOpensPeekId, true);
                _keyboardDevice.SetupGet(x => x.KeyModifiers).Returns(VimKeyModifiers.Control);
                var point = _textView.GetPointInLine(0, 0);
                _testableMouseDevice.Point = point;
                VimHost.GoToDefinitionReturn = false;
                _vimMouseProcessor.TryProcess(VimKey.LeftMouse);
                Assert.Equal(0, VimHost.GoToDefinitionCount);
                Assert.Equal(1, VimHost.PeekDefinitionCount);
            }

            [WpfFact]
            public void Issue1317()
            {
                _vimBuffer.ProcessNotation("v");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimMouseProcessor.TryProcess(VimKey.RightMouse));
                Assert.Equal(0, VimHost.BeepCount);
            }
        }
    }
}
