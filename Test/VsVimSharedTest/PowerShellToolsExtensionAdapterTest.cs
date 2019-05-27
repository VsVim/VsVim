using Moq;
using Xunit;
using Vim.VisualStudio.Implementation.PowerShellTools;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Vim.UnitTest;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class PowerShellToolsExtensionAdapterTest
    {
        private readonly IExtensionAdapter _extensionAdapter;
        private readonly Mock<IPowerShellToolsUtil> _powerShellToolsUtil;

        protected PowerShellToolsExtensionAdapterTest()
        {
            _powerShellToolsUtil = new Mock<IPowerShellToolsUtil>();
            _powerShellToolsUtil.SetupGet(x => x.IsInstalled).Returns(true);
            _extensionAdapter = new PowerShellToolsExtensionAdapter(_powerShellToolsUtil.Object);
        }

        public sealed class ShouldCreateVimBufferTest : PowerShellToolsExtensionAdapterTest
        {
            private readonly Mock<IContentType> _contentType;
            private readonly Mock<ITextBuffer> _textBuffer;
            private readonly Mock<ITextDataModel> _textDataModel;
            private readonly Mock<ITextView> _textView;

            public ShouldCreateVimBufferTest()
            {
                _contentType = new Mock<IContentType>(MockBehavior.Strict);
                _contentType.SetupGet(x => x.DisplayName).Returns("");

                _textBuffer = new Mock<ITextBuffer>(MockBehavior.Strict);
                _textBuffer.SetupGet(x => x.ContentType).Returns(_contentType.Object);

                _textDataModel = new Mock<ITextDataModel>(MockBehavior.Strict);
                _textDataModel.SetupGet(x => x.DocumentBuffer).Returns(_textBuffer.Object);

                _textView = new Mock<ITextView>(MockBehavior.Strict);
                _textView.SetupGet(x => x.TextDataModel).Returns(_textDataModel.Object);
            }

            [WpfFact]
            public void NotInstalled()
            {
                _powerShellToolsUtil.SetupGet(x => x.IsInstalled).Returns(false);
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }

            [WpfFact]
            public void InstalledAndNotInteractiveWindow()
            {
                _powerShellToolsUtil.SetupGet(x => x.IsInstalled).Returns(true);
                _contentType.SetupGet(x => x.DisplayName).Returns("");
                Assert.Null(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }

            [WpfFact]
            public void InstalledAndInteractiveWindow()
            {
                _powerShellToolsUtil.SetupGet(x => x.IsInstalled).Returns(true);
                _contentType.SetupGet(x => x.DisplayName).Returns("PowerShellREPLCode");
                Assert.False(_extensionAdapter.ShouldCreateVimBuffer(_textView.Object));
            }
        }
    }
}
