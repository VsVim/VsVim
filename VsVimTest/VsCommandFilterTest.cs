using VsVim;
using NUnit.Framework;
using System;
using Vim;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using VsVimTest.Utils;

namespace VsVimTest
{
    [TestFixture()]
    public class VsCommandFilterTest
    {
        #region OleCommandTargetUtil

        private class OleCommandTargetUtil : IOleCommandTarget
        {
            public int ExecCount;
            public Guid LastExecCommandGroup;
            public uint LastExecCommandId;
            public uint LastExecCommandExecOpt;
            public IntPtr LastExecInput;
            public IntPtr LastExecOutput;

            public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
            {
                ExecCount++;
                LastExecCommandGroup = pguidCmdGroup;
                LastExecCommandId = nCmdID;
                LastExecCommandExecOpt = nCmdexecopt;
                LastExecInput = pvaIn;
                LastExecOutput = pvaOut;
                return 0;
            }

            public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region VsTextViewUtil

        private class VsTextViewUtil : IVsTextView
        {
            private readonly IOleCommandTarget m_next;

            public VsTextViewUtil(IOleCommandTarget next)
            {
                m_next = next;
            }

            public int AddCommandFilter(IOleCommandTarget pNewCmdTarg, out IOleCommandTarget ppNextCmdTarg)
            {
                ppNextCmdTarg = m_next;
                return 0;
            }

            public int CenterColumns(int iLine, int iLeftCol, int iColCount)
            {
                throw new NotImplementedException();
            }

            public int CenterLines(int iTopLine, int iCount)
            {
                throw new NotImplementedException();
            }

            public int ClearSelection(int fMoveToAnchor)
            {
                throw new NotImplementedException();
            }

            public int CloseView()
            {
                throw new NotImplementedException();
            }

            public int EnsureSpanVisible(TextSpan span)
            {
                throw new NotImplementedException();
            }

            public int GetBuffer(out IVsTextLines ppBuffer)
            {
                throw new NotImplementedException();
            }

            public int GetCaretPos(out int piLine, out int piColumn)
            {
                throw new NotImplementedException();
            }

            public int GetLineAndColumn(int iPos, out int piLine, out int piIndex)
            {
                throw new NotImplementedException();
            }

            public int GetLineHeight(out int piLineHeight)
            {
                throw new NotImplementedException();
            }

            public int GetNearestPosition(int iLine, int iCol, out int piPos, out int piVirtualSpaces)
            {
                throw new NotImplementedException();
            }

            public int GetPointOfLineColumn(int iLine, int iCol, POINT[] ppt)
            {
                throw new NotImplementedException();
            }

            public int GetScrollInfo(int iBar, out int piMinUnit, out int piMaxUnit, out int piVisibleUnits, out int piFirstVisibleUnit)
            {
                throw new NotImplementedException();
            }

            public int GetSelectedText(out string pbstrText)
            {
                throw new NotImplementedException();
            }

            public int GetSelection(out int piAnchorLine, out int piAnchorCol, out int piEndLine, out int piEndCol)
            {
                throw new NotImplementedException();
            }

            public int GetSelectionDataObject(out IDataObject ppIDataObject)
            {
                throw new NotImplementedException();
            }

            public TextSelMode GetSelectionMode()
            {
                throw new NotImplementedException();
            }

            public int GetSelectionSpan(TextSpan[] pSpan)
            {
                throw new NotImplementedException();
            }

            public int GetTextStream(int iTopLine, int iTopCol, int iBottomLine, int iBottomCol, out string pbstrText)
            {
                throw new NotImplementedException();
            }

            public IntPtr GetWindowHandle()
            {
                throw new NotImplementedException();
            }

            public int GetWordExtent(int iLine, int iCol, uint dwFlags, TextSpan[] pSpan)
            {
                throw new NotImplementedException();
            }

            public int HighlightMatchingBrace(uint dwFlags, uint cSpans, TextSpan[] rgBaseSpans)
            {
                throw new NotImplementedException();
            }

            public int Initialize(IVsTextLines pBuffer, IntPtr hwndParent, uint InitFlags, INITVIEW[] pInitView)
            {
                throw new NotImplementedException();
            }

            public int PositionCaretForEditing(int iLine, int cIndentLevels)
            {
                throw new NotImplementedException();
            }

            public int RemoveCommandFilter(IOleCommandTarget pCmdTarg)
            {
                throw new NotImplementedException();
            }

            public int ReplaceTextOnLine(int iLine, int iStartCol, int iCharsToReplace, string pszNewText, int iNewLen)
            {
                throw new NotImplementedException();
            }

            public int RestrictViewRange(int iMinLine, int iMaxLine, IVsViewRangeClient pClient)
            {
                throw new NotImplementedException();
            }

            public int SendExplicitFocus()
            {
                throw new NotImplementedException();
            }

            public int SetBuffer(IVsTextLines pBuffer)
            {
                throw new NotImplementedException();
            }

            public int SetCaretPos(int iLine, int iColumn)
            {
                throw new NotImplementedException();
            }

            public int SetScrollPosition(int iBar, int iFirstVisibleUnit)
            {
                throw new NotImplementedException();
            }

            public int SetSelection(int iAnchorLine, int iAnchorCol, int iEndLine, int iEndCol)
            {
                throw new NotImplementedException();
            }

            public int SetSelectionMode(TextSelMode iSelMode)
            {
                throw new NotImplementedException();
            }

            public int SetTopLine(int iBaseLine)
            {
                throw new NotImplementedException();
            }

            public int UpdateCompletionStatus(IVsCompletionSet pCompSet, uint dwFlags)
            {
                throw new NotImplementedException();
            }

            public int UpdateTipWindow(IVsTipWindow pTipWindow, uint dwFlags)
            {
                throw new NotImplementedException();
            }

            public int UpdateViewFrameCaption()
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        private Mock<IVimBuffer> m_buffer;
        private OleCommandTargetUtil m_util;
        private VsTextViewUtil m_view;
        private VsCommandFilter m_filterRaw;
        private IOleCommandTarget m_filter;

        [SetUp]
        public void Init()
        {
            m_buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            m_util = new OleCommandTargetUtil();
            m_view = new VsTextViewUtil(m_util);
            m_filterRaw = new VsCommandFilter(m_buffer.Object, m_view);
            m_filter = m_filterRaw;
        }

        [Test(), Description("Don't process text input in insert mode"),Ignore]
        public void Exec1()
        {
            m_buffer.Setup(x => x.ModeKind).Returns(ModeKind.Normal);
            m_buffer.Setup(x => x.WillProcessInput(It.IsAny<KeyInput>())).Returns(false);
            m_buffer.Setup(x => x.ModeKind).Returns(ModeKind.Insert);
            var command = (uint)(VSConstants.VSStd2KCmdID.TYPECHAR);
            var commandGroup = VSConstants.VSStd2K;
            using (var ptr = CharPointer.Create('c'))
            {
                Assert.AreEqual(0, m_filter.Exec(ref commandGroup, command, 0, ptr.IntPtr, IntPtr.Zero));
                Assert.AreEqual(1, m_util.ExecCount);
                Assert.AreEqual(command, m_util.LastExecCommandId);
            }
        }

        [Test, Description("Process Delete"),Ignore]
        public void Exec2()
        {
            var ran = false;
            m_buffer.Setup(x => x.ModeKind).Returns(ModeKind.Normal);
            m_buffer.Setup(x => x.WillProcessInput(It.IsAny<KeyInput>())).Returns(true);
            m_buffer.Setup(x => x.ProcessInput(It.IsAny<KeyInput>())).Returns(
                () =>
                {
                    ran = true;
                    return true;
                });
            var command = (uint)(VSConstants.VSStd2KCmdID.DELETE);
            var commandGroup = VSConstants.VSStd2K;
            Assert.AreEqual(0, m_filter.Exec(ref commandGroup, command, 0, IntPtr.Zero, IntPtr.Zero));
            Assert.AreEqual(0, m_util.ExecCount);
            Assert.IsTrue(ran);
        }

        [Test(), Description("Don't process text changes if the mode won't process them"),Ignore]
        public void Exec3()
        {
            m_buffer.Setup(x => x.ModeKind).Returns(ModeKind.Insert);
            m_buffer.Setup(x => x.WillProcessInput(It.IsAny<KeyInput>())).Returns(false);
            var command = (uint)(VSConstants.VSStd2KCmdID.TYPECHAR);
            var commandGroup = VSConstants.VSStd2K;
            using (var ptr = CharPointer.Create('c'))
            {
                Assert.AreEqual(0, m_filter.Exec(ref commandGroup, command, 0, ptr.IntPtr, IntPtr.Zero));
                Assert.AreEqual(1, m_util.ExecCount);
                Assert.AreEqual(command, m_util.LastExecCommandId);
            }
        }

        [Test(), Description("Don't process any change that the mode can't handle"),Ignore]
        public void Exec4()
        {
            m_buffer.Setup(x => x.ModeKind).Returns(ModeKind.Insert);
            m_buffer.Setup(x => x.WillProcessInput(It.IsAny<KeyInput>())).Returns(false);
            var command = (uint)(VSConstants.VSStd2KCmdID.RETURN);
            var commandGroup = VSConstants.VSStd2K;
            Assert.AreEqual(0, m_filter.Exec(ref commandGroup, command, 0, IntPtr.Zero, IntPtr.Zero));
            Assert.AreEqual(1, m_util.ExecCount);
            Assert.AreEqual(command, m_util.LastExecCommandId);
        }
    }
}
