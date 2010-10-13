using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim.UnitTest.Mock;
using VsVim.Implementation;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsAdapterTest
    {
        private MockRepository _factory;
        private Mock<IVsEditorAdaptersFactoryService> _editorAdapterFactory;
        private Mock<IEditorOptionsFactoryService> _editorOptionsFactory;
        private Mock<SVsServiceProvider> _serviceProvider;
        private VsAdapter _adapterRaw;
        private IVsAdapter _adapter;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _editorAdapterFactory = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorOptionsFactory = _factory.Create<IEditorOptionsFactoryService>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider.MakeService<SVsTextManager, IVsTextManager>(_factory);
            _serviceProvider.MakeService<SVsUIShell, IVsUIShell>(_factory);
            _serviceProvider.MakeService<SVsRunningDocumentTable, IVsRunningDocumentTable>(_factory);
            _adapterRaw = new VsAdapter(
                _editorAdapterFactory.Object,
                _editorOptionsFactory.Object,
                _serviceProvider.Object);
            _adapter = _adapterRaw;
        }

        [Test]
        public void IsReadOnly1()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new ArgumentException())
                .Verifiable();
            _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            Assert.IsFalse(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly2()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Throws(new InvalidOperationException())
                .Verifiable();
            _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            Assert.IsFalse(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly3()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly4()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.E_FAIL);
            Assert.IsFalse(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly5()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = 0u;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.IsFalse(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

        [Test]
        public void IsReadOnly6()
        {
            var buffer = _factory.Create<ITextBuffer>();
            var editorOptions = _editorOptionsFactory.MakeOptions(buffer.Object, _factory);
            editorOptions
                .Setup(x => x.GetOptionValue<bool>(DefaultTextViewOptions.ViewProhibitUserInputId))
                .Returns(false)
                .Verifiable();
            var flags = (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
            var textLines = _editorAdapterFactory.MakeBufferAdapter(buffer.Object, _factory);
            textLines
                .Setup(x => x.GetStateFlags(out flags))
                .Returns(VSConstants.S_OK);
            Assert.IsTrue(_adapter.IsReadOnly(buffer.Object));
            _factory.Verify();
        }

    }
}
