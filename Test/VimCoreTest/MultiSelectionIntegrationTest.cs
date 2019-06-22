using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Vim.Extensions;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.FSharp.Core;
using System.Threading.Tasks;

namespace Vim.UnitTest
{
    /// <summary>
    /// Class for testing the full integration story of multiple carets
    /// </summary>
    public abstract class MultiSelectionIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected IVimBufferData _vimBufferData;
        protected IVimTextBuffer _vimTextBuffer;
        protected IWpfTextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimLocalSettings _localSettings;
        protected IVimWindowSettings _windowSettings;
        protected IJumpList _jumpList;
        protected IKeyMap _keyMap;
        protected IVimData _vimData;
        protected INormalMode _normalMode;
        protected MockVimHost _vimHost;
        protected TestableClipboardDevice _clipboardDevice;
        protected TestableMouseDevice _testableMouseDevice;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBufferData = _vimBuffer.VimBufferData;
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _normalMode = _vimBuffer.NormalMode;
            _keyMap = _vimBuffer.Vim.KeyMap;
            _localSettings = _vimBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _windowSettings = _vimBuffer.WindowSettings;
            _jumpList = _vimBuffer.JumpList;
            _vimHost = (MockVimHost)_vimBuffer.Vim.VimHost;
            _vimHost.BeepCount = 0;
            _vimHost.IsMultiSelectionSupported = true;
            _vimData = Vim.VimData;
            _clipboardDevice = (TestableClipboardDevice)CompositionContainer.GetExportedValue<IClipboardDevice>();

            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            _testableMouseDevice.YOffset = 0;
        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            base.Dispose();
        }

        private void AssertCaret(VirtualSnapshotPoint caretPoint, SelectedSpan selectedSpan)
        {
            Assert.Equal(caretPoint, selectedSpan.CaretPoint);
            Assert.Equal(caretPoint, selectedSpan.AnchorPoint);
            Assert.Equal(caretPoint, selectedSpan.ActivePoint);
        }

        public sealed class AddCaretTest : MultiSelectionIntegrationTest
        {
            [WpfFact]
            public void AddCaret()
            {
                Create("cat", "bat", "");
                _vimBuffer.ProcessNotation("<C-A-Down>");
                Assert.Equal(_textView.GetVirtualPointInLine(0, 0), _textView.GetCaretVirtualPoint());
                Assert.Single(_vimHost.SecondarySelectedSpans);
                AssertCaret(_textView.GetVirtualPointInLine(1, 0), _vimHost.SecondarySelectedSpans[0]);
            }
        }
    }
}
