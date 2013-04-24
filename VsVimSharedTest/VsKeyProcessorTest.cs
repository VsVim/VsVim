using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.UI.Wpf;
using Vim.UI.Wpf.UnitTest;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.Implementation.Misc;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class VsKeyProcessorTest : VimKeyProcessorTest
    {
        #region MockAdapter

        internal sealed class MockAdapter : IVsTextView
        {
            public bool SearchInProgress { get; set; }

            #region IVsTextView

            int IVsTextView.AddCommandFilter(Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget pNewCmdTarg, out Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget ppNextCmdTarg)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.CenterColumns(int iLine, int iLeftCol, int iColCount)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.CenterLines(int iTopLine, int iCount)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.ClearSelection(int fMoveToAnchor)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.CloseView()
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.EnsureSpanVisible(TextSpan span)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetBuffer(out IVsTextLines ppBuffer)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetCaretPos(out int piLine, out int piColumn)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetLineAndColumn(int iPos, out int piLine, out int piIndex)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetLineHeight(out int piLineHeight)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetNearestPosition(int iLine, int iCol, out int piPos, out int piVirtualSpaces)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetPointOfLineColumn(int iLine, int iCol, Microsoft.VisualStudio.OLE.Interop.POINT[] ppt)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetScrollInfo(int iBar, out int piMinUnit, out int piMaxUnit, out int piVisibleUnits, out int piFirstVisibleUnit)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetSelectedText(out string pbstrText)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetSelection(out int piAnchorLine, out int piAnchorCol, out int piEndLine, out int piEndCol)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetSelectionDataObject(out Microsoft.VisualStudio.OLE.Interop.IDataObject ppIDataObject)
            {
                throw new System.NotImplementedException();
            }

            TextSelMode IVsTextView.GetSelectionMode()
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetSelectionSpan(TextSpan[] pSpan)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetTextStream(int iTopLine, int iTopCol, int iBottomLine, int iBottomCol, out string pbstrText)
            {
                throw new System.NotImplementedException();
            }

            System.IntPtr IVsTextView.GetWindowHandle()
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.GetWordExtent(int iLine, int iCol, uint dwFlags, TextSpan[] pSpan)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.HighlightMatchingBrace(uint dwFlags, uint cSpans, TextSpan[] rgBaseSpans)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.Initialize(IVsTextLines pBuffer, System.IntPtr hwndParent, uint InitFlags, INITVIEW[] pInitView)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.PositionCaretForEditing(int iLine, int cIndentLevels)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.RemoveCommandFilter(Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget pCmdTarg)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.ReplaceTextOnLine(int iLine, int iStartCol, int iCharsToReplace, string pszNewText, int iNewLen)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.RestrictViewRange(int iMinLine, int iMaxLine, IVsViewRangeClient pClient)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SendExplicitFocus()
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetBuffer(IVsTextLines pBuffer)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetCaretPos(int iLine, int iColumn)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetScrollPosition(int iBar, int iFirstVisibleUnit)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetSelection(int iAnchorLine, int iAnchorCol, int iEndLine, int iEndCol)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetSelectionMode(TextSelMode iSelMode)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.SetTopLine(int iBaseLine)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.UpdateCompletionStatus(IVsCompletionSet pCompSet, uint dwFlags)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.UpdateTipWindow(IVsTipWindow pTipWindow, uint dwFlags)
            {
                throw new System.NotImplementedException();
            }

            int IVsTextView.UpdateViewFrameCaption()
            {
                throw new System.NotImplementedException();
            }

            #endregion
        }

        #endregion

        private MockRepository _factory;
        private IWpfTextView _wpfTextView;
        private Mock<IVimBuffer> _mockVimBuffer;
        private Mock<IVsAdapter> _vsAdapter;
        private Mock<IVsEditorAdaptersFactoryService> _editorAdaptersFactoryService;
        private Mock<IReportDesignerUtil> _reportDesignerUtil;
        internal MockAdapter _mockAdapter;
        internal IVimBufferCoordinator _bufferCoordinator;
        private MockKeyboardDevice _device;

        internal VsKeyProcessor VsKeyProcessor
        {
            get { return (VsKeyProcessor)_processor; }
        }

        protected override VimKeyProcessor CreateKeyProcessor()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _wpfTextView = CreateTextView();
            _mockAdapter = new MockAdapter();
            _editorAdaptersFactoryService = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorAdaptersFactoryService.Setup(x => x.GetViewAdapter(_wpfTextView)).Returns(_mockAdapter);

            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
            _vsAdapter.SetupGet(x => x.EditorAdapter).Returns(_editorAdaptersFactoryService.Object);
            _vsAdapter.Setup(x => x.IsReadOnly(_wpfTextView)).Returns(false);
            _reportDesignerUtil = _factory.Create<IReportDesignerUtil>();
            _reportDesignerUtil.Setup(x => x.IsExpressionView(_wpfTextView)).Returns(false);
            _mockVimBuffer = MockObjectFactory.CreateVimBuffer(_wpfTextView);
            _mockVimBuffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            _mockVimBuffer.Setup(x => x.Process(It.IsAny<KeyInput>())).Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
            _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _bufferCoordinator = new VimBufferCoordinator(_mockVimBuffer.Object);
            _device = new MockKeyboardDevice();
            return new VsKeyProcessor(_vsAdapter.Object, _bufferCoordinator, KeyUtil, _reportDesignerUtil.Object);
        }

        public sealed class VsKeyDownTest : VsKeyProcessorTest
        {
            private void VerifyHandle(Key key, ModifierKeys modKeys = ModifierKeys.None)
            {
                VerifyCore(key, modKeys, shouldHandle: true);
            }

            private void VerifyNotHandle(Key key, ModifierKeys modKeys = ModifierKeys.None)
            {
                VerifyCore(key, modKeys, shouldHandle: false);
            }

            private void VerifyCore(Key key, ModifierKeys modKeys, bool shouldHandle)
            {
                var args = _device.CreateKeyEventArgs(key, modKeys);
                _processor.KeyDown(args);
                Assert.Equal(shouldHandle, args.Handled);
            }

            /// <summary>
            /// Don't handle the AltGr scenarios here.  The AltGr key is just too ambiguous to handle in the 
            /// KeyDown event
            /// </summary>
            [Fact]
            public void AltGr()
            {
                VerifyNotHandle(Key.D, ModifierKeys.Alt | ModifierKeys.Control);
            }

            /// <summary>
            /// Don't handle any alpha input in the KeyDown phase.  This should all be handled inside
            /// of the TextInput phase instead
            /// </summary>
            [Fact]
            public void AlphaKeys()
            {
                VerifyNotHandle(Key.A);
                VerifyNotHandle(Key.B);
                VerifyNotHandle(Key.D1);
                VerifyNotHandle(Key.A, ModifierKeys.Shift);
                VerifyNotHandle(Key.B, ModifierKeys.Shift);
                VerifyNotHandle(Key.D1, ModifierKeys.Shift);
            }

            /// <summary>
            /// If incremental search is active then we don't want to route input to VsVim.  Instead we 
            /// want to let it get processed by incremental search
            /// </summary>
            [Fact]
            public void DontHandleIfIncrementalSearchActive()
            {
                var all = new [] { Key.Enter, Key.Tab, Key.Back };
                foreach (var key in all)
                {
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
                    VerifyHandle(key);
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                    VerifyNotHandle(key);
                }
            }
        }

        public sealed class VsTextInputTest : VsKeyProcessorTest
        {
            private TextCompositionEventArgs CreateTextComposition(string text)
            {
                return _wpfTextView.VisualElement.CreateTextCompositionEventArgs(text, _device);
            }

            private void VerifyHandle(string text)
            {
                VerifyCore(text, shouldHandle: true);
            }

            private void VerifyNotHandle(string text)
            {
                VerifyCore(text, shouldHandle: false);
            }

            private void VerifyCore(string text, bool shouldHandle)
            {
                var args = CreateTextComposition(text);
                _processor.TextInput(args);
                Assert.Equal(shouldHandle, args.Handled);
            }

            /// <summary>
            /// Make sure that alpha input is handled in TextInput
            /// </summary>
            [Fact]
            public void AlphaKeys()
            {
                var all = "ab1AB!";
                foreach (var current in all)
                {
                    VerifyHandle(current.ToString());
                }
            }

            /// <summary>
            /// If incremental search is active then we don't want to route input to VsVim.  Instead we 
            /// want to let it get processed by incremental search
            /// </summary>
            [Fact]
            public void DontHandleIfIncrementalSearchActive()
            {
                var all = new [] { KeyInputUtil.EnterKey, KeyInputUtil.CharToKeyInput('a') };
                foreach (var keyInput in all)
                {
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);
                    VerifyHandle(keyInput.Char.ToString());
                    _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                    VerifyNotHandle(keyInput.Char.ToString());
                }
            }

            /// <summary>
            /// When presented with a KeyInput the TryProcess command should consider if the mapped key
            /// is a direct insert not the provided key.  
            /// </summary>
            [Fact]
            public void InsertCheckShouldConsiderMapped()
            {
                var keyInput = KeyInputUtil.CharWithControlToKeyInput('e');
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _mockVimBuffer.Setup(x => x.CanProcessAsCommand(keyInput)).Returns(true).Verifiable();
                VerifyHandle(keyInput.Char.ToString());
                _factory.Verify();
            }

            /// <summary>
            /// We only do the CanProcessAsCommand check in insert mode.  The justification is that direct
            /// insert commands should go through IOleCommandTarget in order to trigger intellisense and
            /// the like.  If we're not in insert mode we don't consider intellisense in the key 
            /// processor
            /// </summary>
            [Fact]
            public void NonInsertShouldntCheckForCommand()
            {
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
                VerifyHandle(KeyInputUtil.CharWithControlToKeyInput('e').Char.ToString());
                _factory.Verify();
            }

            /// <summary>
            /// Visual Studio won't pass along any characters less or equal to 0x1f so we need to
            /// handle them no matter what mode we are in
            /// </summary>
            [Fact]
            public void LowerControlKeys()
            {
                const int max = 0x1f;
                var count = 0;
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _mockVimBuffer.Setup(x => x.CanProcessAsCommand(It.IsAny<KeyInput>())).Returns(false);
                _mockVimBuffer
                    .Setup(x => x.Process(It.IsAny<KeyInput>()))
                    .Callback(() => { count++; })
                    .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
                for (var i = 1; i <= max; i++)
                {
                    var c = (char)i;
                    var text = c.ToString();
                    VerifyHandle(text);
                }
                Assert.Equal(max, count);
            }

            /// <summary>
            /// Visual Studio will pass along other control characters so don't handle them.  Let them
            /// go through intellisense
            /// </summary>
            [Fact]
            public void UpperControlKeys()
            {
                const int start = 0x20;
                var count = 0;
                _mockVimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _mockVimBuffer.Setup(x => x.CanProcessAsCommand(It.IsAny<KeyInput>())).Returns(false);
                _mockVimBuffer
                    .Setup(x => x.Process(It.IsAny<KeyInput>()))
                    .Callback(() => { count++; })
                    .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
                for (var i = start; i <= start + 10; i++)
                {
                    var c = (char)i;
                    var text = c.ToString();
                    VerifyNotHandle(text);
                }
                Assert.Equal(0, count);
            }
        }

        /// <summary>
        /// Test the work around for the VsCodeWindowAdapter::PreProcessMessage override which 
        /// causes the TextInput event to not fire when the buffer is considered read only 
        /// </summary>
        public sealed class ReadOnlyTest : VsKeyProcessorTest
        {
            public ReadOnlyTest()
            {
                _vsAdapter.Setup(x => x.IsReadOnly(_wpfTextView)).Returns(true);
            }

            /// <summary>
            /// Suppress the VsCodeWindowAdapter handling on KeyDown.  The WM_CHAR message will
            /// come between the KeyDown and KeyUp event
            /// </summary>
            [Fact]
            public void SuppressOnKeyDown()
            {
                var e = _device.CreateKeyEventArgs(Key.A);
                _processor.KeyDown(e);
                Assert.True(_mockAdapter.SearchInProgress);
            }

            /// <summary>
            /// Restore the handling on KeyUp.  
            /// </summary>
            [Fact]
            public void RestoreOnKeyUp()
            {
                var e = _device.CreateKeyEventArgs(Key.A);
                _processor.KeyDown(e);
                _processor.KeyUp(e);
                Assert.False(_mockAdapter.SearchInProgress);
            }

            /// <summary>
            /// So long as keys remain down we want to suppress the PreProcessMessage path
            /// </summary>
            [Fact]
            public void MoreKeyDowns()
            {
                var e = _device.CreateKeyEventArgs(Key.A);
                _processor.KeyDown(e);
                _processor.KeyDown(e);
                _processor.KeyUp(e);
                Assert.Equal(1, VsKeyProcessor.KeyDownCount);
                Assert.True(_mockAdapter.SearchInProgress);
            }

            /// <summary>
            /// Possible that we entered the state with keys down that we weren't tracking and
            /// hence we'll end up with lots of up messages.  If that happens then we just ignore
            /// them
            /// </summary>
            [Fact]
            public void MoreKeyUps()
            {
                var e = _device.CreateKeyEventArgs(Key.A);
                _processor.KeyDown(e);
                _processor.KeyUp(e);
                _processor.KeyUp(e);
                Assert.Equal(0, VsKeyProcessor.KeyDownCount);
                Assert.False(_mockAdapter.SearchInProgress);
            }

            /// <summary>
            /// Handle the case where the buffer suddenly changes to not readonly in a KeyDown
            /// while we're in the middle of suppressing messages
            /// </summary>
            [Fact]
            public void ChangeInReadOnlyOnDown()
            {
                var e = _device.CreateKeyEventArgs(Key.A);
                _processor.KeyDown(e);
                _vsAdapter.Setup(x => x.IsReadOnly(_wpfTextView)).Returns(false);
                _processor.KeyDown(e);
                Assert.Equal(0, VsKeyProcessor.KeyDownCount);
                Assert.False(_mockAdapter.SearchInProgress);
            }
        }

        public sealed class ReportDesignerTest : VsKeyProcessorTest
        {
            /// <summary>
            /// If it isn't an expression view then it should be processed even if the key is in the 
            /// required set 
            /// </summary>
            [Fact]
            public void NullCase()
            {
                _reportDesignerUtil.Setup(x => x.IsExpressionView(_wpfTextView)).Returns(false);
                var e = _device.CreateKeyEventArgs(Key.Back);
                _processor.KeyDown(e);
                Assert.True(e.Handled);
            }

            [Fact]
            public void NotSpecialKey()
            {
                _reportDesignerUtil.Setup(x => x.IsExpressionView(_wpfTextView)).Returns(true);
                _reportDesignerUtil.Setup(x => x.IsSpecialHandled(It.IsAny<KeyInput>())).Returns(false);
                var e = _device.CreateKeyEventArgs(Key.Back);
                _processor.KeyDown(e);
                Assert.True(e.Handled);
            }

            [Fact]
            public void SpecialKey()
            {
                _reportDesignerUtil.Setup(x => x.IsExpressionView(_wpfTextView)).Returns(true);
                _reportDesignerUtil.Setup(x => x.IsSpecialHandled(It.IsAny<KeyInput>())).Returns(true);
                var e = _device.CreateKeyEventArgs(Key.Back);
                _processor.KeyDown(e);
                Assert.False(e.Handled);
            }
        }
    }
}
