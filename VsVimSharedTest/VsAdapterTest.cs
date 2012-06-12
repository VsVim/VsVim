using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;
using VsVim.Implementation;
using Microsoft.VisualStudio.Text.IncrementalSearch;

namespace VsVim.UnitTest
{
    public sealed class VsAdapterTest
    {
        private readonly MockRepository _factory;
        private readonly Mock<IVsEditorAdaptersFactoryService> _editorAdapterFactory;
        private readonly Mock<IEditorOptionsFactoryService> _editorOptionsFactory;
        private readonly Mock<IIncrementalSearchFactoryService> _incrementalSearchFactoryService;
        private readonly Mock<IPowerToolsUtil> _powerToolsUtil;
        private readonly Mock<SVsServiceProvider> _serviceProvider;
        private readonly VsAdapter _adapterRaw;
        private readonly IVsAdapter _adapter;

        public VsAdapterTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _editorAdapterFactory = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorOptionsFactory = _factory.Create<IEditorOptionsFactoryService>();
            _incrementalSearchFactoryService = _factory.Create<IIncrementalSearchFactoryService>();
            _powerToolsUtil = _factory.Create<IPowerToolsUtil>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider.MakeService<SVsTextManager, IVsTextManager>(_factory);
            _serviceProvider.MakeService<SVsUIShell, IVsUIShell>(_factory);
            _serviceProvider.MakeService<SVsRunningDocumentTable, IVsRunningDocumentTable>(_factory);
            _adapterRaw = new VsAdapter(
                _editorAdapterFactory.Object,
                _editorOptionsFactory.Object,
                _incrementalSearchFactoryService.Object,
                _powerToolsUtil.Object,
                _serviceProvider.Object);
            _adapter = _adapterRaw;
        }

        [Fact]
        public void IsReadOnly1()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new ArgumentException())
                .Verifiable();
            _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            Assert.False(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Fact]
        public void IsReadOnly2()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new InvalidOperationException())
                .Verifiable();
            _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            Assert.False(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Fact]
        public void IsReadOnly3()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(true)
                .Verifiable();
            Assert.True(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Fact]
        public void IsReadOnly4()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.E_FAIL);
            Assert.False(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Fact]
        public void IsReadOnly5()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.False(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Fact]
        public void IsReadOnly6()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.IsOptionDefined<bool>(DefaultTextViewOptions.ViewProhibitUserInputId, false))
                .Returns(true)
                .Verifiable();
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.True(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        /// <summary>
        /// The power tools quick find is considered an incremental search 
        /// </summary>
        [Fact]
        public void IsIncrementalSearchActive_PowerTools()
        {
            _powerToolsUtil.SetupGet(x => x.IsQuickFindActive).Returns(true);

            var textView = _factory.Create<ITextView>().Object;
            Assert.True(_adapter.IsIncrementalSearchActive(textView));
        }
    }
}
