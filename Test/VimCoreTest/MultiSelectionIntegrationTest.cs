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
using System.Collections.Generic;

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
        protected IVimHost _vimHost;
        protected MockVimHost _mockVimHost;
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
            _vimHost = _vimBuffer.Vim.VimHost;
            _mockVimHost = (MockVimHost)_vimHost;
            _mockVimHost.BeepCount = 0;
            _mockVimHost.IsMultiSelectionSupported = true;
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

        private VirtualSnapshotPoint[] CaretPoints =>
            _vimHost.GetSelectedSpans(_textView).Select(x => x.CaretPoint).ToArray();

        private SelectedSpan[] SelectedSpans =>
            _vimHost.GetSelectedSpans(_textView).ToArray();

        private VirtualSnapshotPoint GetPoint(int lineNumber, int column)
        {
            return _textView.GetVirtualPointInLine(lineNumber, column);
        }

        private void SetCaretPoints(IEnumerable<VirtualSnapshotPoint> caretPoints)
        {
            _vimHost.SetSelectedSpans(_textView, caretPoints.Select(x => new SelectedSpan(x)));
        }

        private void SetCaretPoints(params VirtualSnapshotPoint[] caretPoints)
        {
            SetCaretPoints(caretPoints.AsEnumerable());
        }

        public sealed class MockTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Mock inftrastructure should use the real text view for the
            /// primary selection and the internal data structure for the
            /// secondary selection
            /// </summary>
            [WpfFact]
            public void Basic()
            {
                Create("cat", "bat", "");
                SetCaretPoints(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetVirtualPointInLine(1, 1));
                var spans = GetSelectedSpans();

                // Verify real caret and real selection.
                Assert.Equal(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetCaretVirtualPoint());
                Assert.Equal(
                    new VirtualSnapshotSpan(new SnapshotSpan(_textView.GetPointInLine(0, 1), 0)),
                    _textView.GetVirtualSelectionSpan());

                // Verify secondary selection agrees with mock vim host.
                Assert.Single(_mockVimHost.SecondarySelectedSpans);
                Assert.Equal(
                    new SelectedSpan(_textView.GetVirtualPointInLine(1, 1)),
                    _mockVimHost.SecondarySelectedSpans[0]);
                Assert.Equal(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetCaretVirtualPoint());
                Assert.Equal(
                    new SelectedSpan(_textView.GetVirtualPointInLine(1, 1)),
                    spans[1]);
            }
        }

        public sealed class AddCaretTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Test adding a caret with ctrl-alt-arrow
            /// </summary>
            [WpfFact]
            public void AddCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _vimBuffer.ProcessNotation("<C-A-Down>");
                var spans = GetSelectedSpans();
                Assert.Equal(2, spans.Length);
                Assert.Equal(GetPoint(0, 0).GetSelectedSpan(), spans[0]);
                Assert.Equal(GetPoint(1, 0).GetSelectedSpan(), spans[1]);
            }
        }

        public sealed class SelectModeTest : MultiSelectionIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _globalSettings.Selection = "exclusive";
            }

            /// <summary>
            /// Test entering select mode
            /// </summary>
            [WpfFact]
            public void Enter()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                _vimBuffer.ProcessNotation("<S-Right>");
                var spans = GetSelectedSpans();
                Assert.Equal(2, spans.Length);
                Assert.Equal(GetPoint(0, 1).GetSelectedSpan(-1, 0), spans[0]);
                Assert.Equal(GetPoint(1, 1).GetSelectedSpan(-1, 0), spans[1]);
            }
        }
    }
}
