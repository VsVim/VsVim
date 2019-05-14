using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using System;
using System.Collections.Generic;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.ExternalEdit;
using Vim.VisualStudio.Implementation.ReSharper;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class ReSharperExtensionAdapterTest 
    {
        private readonly IExtensionAdapter _extensionAdapter;
        private readonly Mock<IReSharperUtil> _resharperUtil;
        private readonly Mock<ITextDocumentFactoryService> _textDocumentFactoryService;

        protected ReSharperExtensionAdapterTest()
        {
            _resharperUtil = new Mock<IReSharperUtil>();
            _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true);
            _textDocumentFactoryService = new Mock<ITextDocumentFactoryService>();
            _extensionAdapter = new ReSharperExtensionAdapter(_resharperUtil.Object, _textDocumentFactoryService.Object);
        }

        public sealed class ShouldCreateVimBufferTest : ReSharperExtensionAdapterTest
        {
            private readonly Mock<ITextView> _textView;
            private readonly Mock<ITextBuffer> _textBuffer;
            private readonly Mock<ITextDataModel> _textDataModel;
            private readonly Mock<ITextDocument> _textDocument;

            private void SetupPair(ITextBuffer textBuffer, ITextDocument textDocument)
            {
                _textDocumentFactoryService
                    .Setup(x => x.TryGetTextDocument(_textBuffer.Object, out textDocument))
                    .Returns(true);
            }

            public ShouldCreateVimBufferTest()
            {
                _textBuffer = new Mock<ITextBuffer>(MockBehavior.Strict);
                _textDataModel = new Mock<ITextDataModel>(MockBehavior.Strict);
                _textDataModel.SetupGet(x => x.DocumentBuffer).Returns(_textBuffer.Object);

                _textView = new Mock<ITextView>(MockBehavior.Strict);
                _textView.SetupGet(x => x.TextDataModel).Returns(_textDataModel.Object);

                _textDocument = new Mock<ITextDocument>(MockBehavior.Strict);
                _textDocument.SetupGet(x => x.FilePath).Returns("");

                SetupPair(_textBuffer.Object, _textDocument.Object);
            }

            [WpfFact]
            public void NormalNotInstalled()
            {
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(false);
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }

            [WpfFact]
            public void NormalInstalled()
            {
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true);
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }

            [WpfFact]
            public void RegexEditor()
            {
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(false);
                _textDocument.SetupGet(x => x.FilePath).Returns(ReSharperExtensionAdapter.FilePathPrefixRegexEditor);
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));

                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true);
                _textDocument.SetupGet(x => x.FilePath).Returns(ReSharperExtensionAdapter.FilePathPrefixRegexEditor);
                Assert.False(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }

            [WpfFact]
            public void UnitTestSessionsWindow()
            {
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(false);
                _textDocument.SetupGet(x => x.FilePath).Returns(ReSharperExtensionAdapter.FilePathPrefixUnitTestSessionOutput);
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));

                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true);
                _textDocument.SetupGet(x => x.FilePath).Returns(ReSharperExtensionAdapter.FilePathPrefixUnitTestSessionOutput);
                Assert.False(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }
        }
    }
}
