using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using Moq;
using Vim;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using VimCoreTest.Utils;

namespace VsVimTest
{
    [TestFixture] 
    public class VsVimFactoryServiceTest
    {
        private VsVimFactoryService _serviceRaw;
        private IVsVimFactoryService _service;
        private Mock<IVsEditorAdaptersFactoryService> _adapters;
        private Mock<IVimHost> _vimHost;
        private Mock<IVimFactoryService> _vimFactoryService;
        private Mock<IVim> _vim;
        private FakeServiceProvider _sp;

        private void Create()
        {
            _adapters = new Mock<IVsEditorAdaptersFactoryService>(MockBehavior.Strict);
            _vimHost = new Mock<IVimHost>(MockBehavior.Strict);
            _vim = new Mock<IVim>(MockBehavior.Strict);
            _vimFactoryService = new Mock<IVimFactoryService>(MockBehavior.Strict);
            _vimFactoryService.SetupGet(x => x.Vim).Returns(_vim.Object);
            _serviceRaw = new VsVimFactoryService(
                _vimFactoryService.Object,
                _adapters.Object,
                _vimHost.Object);
            _service = _serviceRaw;
            _sp = new FakeServiceProvider();
            _serviceRaw.ServiceProvider = _sp;
        }

        [Test]
        public void VimFactoryService1()
        {
            Create();
            Assert.AreSame(_vimFactoryService.Object, _service.VimFactoryService);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GetOrCreateBuffer1()
        {
            Create();
            var buffer = new Mock<ITextBuffer>(MockBehavior.Strict);
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            view.SetupGet(x => x.TextBuffer).Returns(buffer.Object);
            _adapters.Setup(x => x.GetBufferAdapter(buffer.Object)).Returns((IVsTextBuffer)null);
            _service.GetOrCreateBuffer(view.Object);
        }

        [Test]
        public void GetOrCreateBuffer2()
        {
            Create();
            var view = EditorUtil.CreateView("foo bar");
            var lines = new Mock<IVsTextLines>(MockBehavior.Strict);
            _adapters.Setup(x => x.GetBufferAdapter(view.TextBuffer)).Returns(lines.Object);
        }

    }
}
