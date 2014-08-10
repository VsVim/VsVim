using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Xunit;
using Moq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest;
using EditorUtils;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class VimHostTest : VimTestBase
    {
        #region VimHostImpl

        private sealed class VimHostImpl : VimHost
        {
            internal VimHostImpl(
                ITextBufferFactoryService textBufferFactoryService,
                ITextEditorFactoryService textEditorFactoryService,
                ITextDocumentFactoryService textDocumentFactoryService,
                IEditorOperationsFactoryService editorOperationsFactoryService) :
                base(textBufferFactoryService, textEditorFactoryService, textDocumentFactoryService, editorOperationsFactoryService)
            {

            }

            public override void FormatLines(ITextView textView, EditorUtils.SnapshotLineRange range)
            {
                throw new NotImplementedException();
            }

            public override string GetName(ITextBuffer value)
            {
                throw new NotImplementedException();
            }

            public override bool GoToDefinition()
            {
                throw new NotImplementedException();
            }

            public override bool GoToGlobalDeclaration(ITextView textView, string name)
            {
                throw new NotImplementedException();
            }

            public override bool GoToLocalDeclaration(ITextView textView, string name)
            {
                throw new NotImplementedException();
            }

            public override void GoToTab(int index)
            {
                throw new NotImplementedException();
            }

            public override bool GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
            {
                return false;
            }

            public override bool LoadFileIntoExistingWindow(string filePath, ITextView textView)
            {
                throw new NotImplementedException();
            }

            public override bool LoadFileIntoNewWindow(string filePath)
            {
                throw new NotImplementedException();
            }

            public override void Make(bool jumpToFirstError, string arguments)
            {
                throw new NotImplementedException();
            }

            public override void MoveFocus(ITextView textView, Direction direction)
            {
                throw new NotImplementedException();
            }

            public override bool NavigateTo(VirtualSnapshotPoint point)
            {
                throw new NotImplementedException();
            }

            public override void RunVisualStudioCommand(ITextView textView, string command, string argument)
            {
                throw new NotImplementedException();
            }

            public override void SplitViewHorizontally(ITextView value)
            {
                throw new NotImplementedException();
            }

            public override void SplitViewVertically(ITextView value)
            {
                throw new NotImplementedException();
            }

            public override int TabCount
            {
                get { throw new NotImplementedException(); }
            }

            public override int GetTabIndex(ITextView textView)
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        private readonly VimHost _vimHost;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;

        protected VimHostTest()
        {
            _textDocumentFactoryService = CompositionContainer.GetExportedValue<ITextDocumentFactoryService>();
            _vimHost = new VimHostImpl(
                TextBufferFactoryService,
                TextEditorFactoryService,
                _textDocumentFactoryService,
                EditorOperationsFactoryService);
        }

        public sealed class RunCommandTests : VimHostTest
        {
            [Fact]
            public void ItUsesWorkingDirectoryFromVimData()
            {
                const string cwd = @"C:\Windows";
                var vimHost = new Mock<VimHost>(Mock.Of<ITextBufferFactoryService>(), 
                                          Mock.Of<ITextEditorFactoryService>(),
                                          Mock.Of<ITextDocumentFactoryService>(),
                                          Mock.Of<IEditorOperationsFactoryService>()){CallBase = true}.Object;
                var vimData = Mock.Of<IVimData>(x => x.CurrentDirectory == cwd);

                vimHost.RunCommand("pwd", "", vimData);

                // Not sure if we can do anything besides verify that it used the getter
                Mock.Get(vimData).VerifyGet(x => x.CurrentDirectory);
            }
        }

        public sealed class IsDirtyTest : VimHostTest
        {
            private ITextDocument CreateTextDocument(params string[] lines)
            {
                var textBuffer = CreateTextBuffer(lines);
                return CreateTextDocument(textBuffer);
            }

            private ITextDocument CreateTextDocument(ITextBuffer textBuffer)
            {
                var name = Guid.NewGuid().ToString();
                return _textDocumentFactoryService.CreateTextDocument(textBuffer, name);
            }

            [Fact]
            public void NormalBufferSaved()
            {
                var textDocument = CreateTextDocument("");
                Assert.False(_vimHost.IsDirty(textDocument.TextBuffer));
            }

            [Fact]
            public void NormalBufferDirty()
            {
                var textDocument = CreateTextDocument("");
                textDocument.TextBuffer.Replace(new Span(0, 0), "hello");
                Assert.True(_vimHost.IsDirty(textDocument.TextBuffer));
            }

            [Fact]
            public void NormalProjectionSaved()
            {
                var textDocument1 = CreateTextDocument("hello world");
                var textDocument2 = CreateTextDocument("big dog");
                var projectionBuffer = CreateProjectionBuffer(textDocument1.TextBuffer.GetExtent(), textDocument2.TextBuffer.GetExtent());
                Assert.False(_vimHost.IsDirty(projectionBuffer));
            }

            [Fact]
            public void NormalProjectionFirstDirty()
            {
                var textDocument1 = CreateTextDocument("hello world");
                var textDocument2 = CreateTextDocument("big dog");
                var projectionBuffer = CreateProjectionBuffer(textDocument1.TextBuffer.GetExtent(), textDocument2.TextBuffer.GetExtent());
                textDocument1.TextBuffer.Replace(new Span(0, 0), "again ");
                Assert.True(_vimHost.IsDirty(projectionBuffer));
            }

            [Fact]
            public void NormalProjectionSecondDirty()
            {
                var textDocument1 = CreateTextDocument("hello world");
                var textDocument2 = CreateTextDocument("big dog");
                var projectionBuffer = CreateProjectionBuffer(textDocument1.TextBuffer.GetExtent(), textDocument2.TextBuffer.GetExtent());
                textDocument2.TextBuffer.Replace(new Span(0, 0), "again ");
                Assert.True(_vimHost.IsDirty(projectionBuffer));
            }

            /// <summary>
            /// A cshtml file is expressed as an IProjectionBuffer instance.  Only the ITextBuffer instances which 
            /// make it up have backing ITextDocument values.  Those need to be checked for dirty, not the 
            /// IProjectionBuffer itself
            /// </summary>
            [Fact]
            public void Issue1143()
            {
                var textDocument1 = CreateTextDocument("hello world");
                var textDocument2 = CreateTextDocument("big dog");
                var projectionBuffer = CreateProjectionBuffer(textDocument1.TextBuffer.GetExtent(), textDocument2.TextBuffer.GetExtent());
                textDocument1.TextBuffer.Replace(new Span(0, 0), "again ");
                Assert.True(_vimHost.IsDirty(projectionBuffer));
            }
        }
    }
}
