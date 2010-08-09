using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim.Implementation;
using VsVim;
using Moq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Editor;
using Vim.UnitTest;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVimTest
{
    [TestFixture]
    public class TextManagerTest
    {
        private MockFactory _factory;
        private Mock<IVsAdapter> _adapter;
        private Mock<SVsServiceProvider> _serviceProvider;
        private Mock<IVsRunningDocumentTable> _table;
        private TextManager _managerRaw;
        private ITextManager _manager;

        [SetUp]
        public void Setup()
        {
            _factory = new MockFactory(MockBehavior.Loose);
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
            _table = _factory.Create<IVsRunningDocumentTable>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider
                .Setup(x => x.GetService(typeof(SVsRunningDocumentTable)))
                .Returns(_table.Object);
            _managerRaw = new TextManager(
                _adapter.Object,
                _serviceProvider.Object);
            _manager = _managerRaw;
        }

        [Test]
        public void SplitView1()
        {
            var view = EditorUtil.CreateView();
            Assert.IsFalse(_manager.SplitView(view));
        }

        [Test]
        public void SplitView2()
        {
            var view = EditorUtil.CreateView();
            IVsCodeWindow codeWindow = _factory.Create<IVsCodeWindow>().Object;
            _adapter.Setup(x => x.TryGetCodeWindow(view, out codeWindow)).Returns(true).Verifiable();
            Assert.IsFalse(_manager.SplitView(view));
            _factory.Verify();
        }

        [Test]
        public void SplitView3()
        {
            var view = EditorUtil.CreateView();
            var mock = _factory.Create<IVsCodeWindow>();
            var commandTarget = mock.As<IOleCommandTarget>();
            IVsCodeWindow codeWindow = mock.Object;
            _adapter.Setup(x => x.TryGetCodeWindow(view, out codeWindow)).Returns(true).Verifiable();
            var id = VSConstants.GUID_VSStandardCommandSet97;
            commandTarget
                .Setup(x => x.Exec(ref id, It.IsAny<uint>(), It.IsAny<uint>(), IntPtr.Zero, IntPtr.Zero))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.IsTrue(_manager.SplitView(view));
            _factory.Verify();
        }

        [Test]
        public void CloseBuffer1()
        {
            var view = EditorUtil.CreateView();
            Assert.IsFalse(_manager.CloseBuffer(view, false));
        }

        [Test]
        public void CloseBuffer2()
        {
            var view = EditorUtil.CreateView();
            var mock = _factory.Create<IVsWindowFrame>();
            mock
                .Setup(x => x.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_PromptSave))
                .Returns(VSConstants.S_OK);
            IVsWindowFrame frame = mock.Object;
            _adapter
                .Setup(x => x.TryGetContainingWindowFrame(view, out frame))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_manager.CloseBuffer(view, checkDirty:true));
            _factory.Verify();
        }

        [Test]
        public void CloseBuffer3()
        {
            var view = EditorUtil.CreateView();
            var mock = _factory.Create<IVsWindowFrame>();
            mock
                .Setup(x => x.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_SaveIfDirty))
                .Returns(VSConstants.S_OK);
            IVsWindowFrame frame = mock.Object;
            _adapter
                .Setup(x => x.TryGetContainingWindowFrame(view, out frame))
                .Returns(true)
                .Verifiable();
            Assert.IsTrue(_manager.CloseBuffer(view, checkDirty:false));
            _factory.Verify();
        }

        [Test]
        public void CloseView1()
        {
            var view = EditorUtil.CreateView();
            Assert.IsFalse(_manager.CloseView(view, checkDirty:false));
        }

        [Test]
        public void CloseView2()
        {
            var view = EditorUtil.CreateView();
            var tuple = _adapter.MakeCodeWindowAndCommandTarget(view, _factory);
            tuple.Item1.MakeSplit(_adapter);
            var commandTarget = tuple.Item2;
            var id = VSConstants.GUID_VSStandardCommandSet97;
            commandTarget
                .Setup(x => x.Exec(ref id, It.IsAny<uint>(), It.IsAny<uint>(), IntPtr.Zero, IntPtr.Zero))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.IsTrue(_manager.CloseView(view, checkDirty:false));
            _factory.Verify();
        }

        [Test]
        public void MoveViewUp1()
        {
            var view = EditorUtil.CreateView();
            Assert.IsFalse(_manager.MoveViewUp(view));
        }

    }
}
