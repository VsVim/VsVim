using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using VsVim.Implementation.Misc;

namespace VsVim.UnitTest
{
    public class TextManagerTest
    {
        private readonly MockRepository _factory;
        private readonly Mock<IVsAdapter> _adapter;
        private readonly Mock<SVsServiceProvider> _serviceProvider;
        private readonly Mock<IVsRunningDocumentTable> _table;
        private readonly Mock<ISharedService> _sharedService;
        private readonly TextManager _managerRaw;
        private readonly ITextManager _manager;

        public TextManagerTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
            _table = _factory.Create<IVsRunningDocumentTable>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider
                .Setup(x => x.GetService(typeof(SVsRunningDocumentTable)))
                .Returns(_table.Object);
            _sharedService = _factory.Create<ISharedService>();
            _sharedService.Setup(x => x.IsLazyLoaded(It.IsAny<uint>())).Returns(false);
            _managerRaw = new TextManager(
                _adapter.Object,
                _factory.Create<ITextDocumentFactoryService>().Object,
                _factory.Create<ITextBufferFactoryService>().Object,
                _sharedService.Object,
                _serviceProvider.Object);
            _manager = _managerRaw;
        }

        [Fact]
        public void SplitView1()
        {
            var view = _factory.Create<IWpfTextView>();
            Assert.False(_manager.SplitView(view.Object));
        }

        [Fact]
        public void SplitView2()
        {
            var view = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view.Object, _factory);
            Assert.False(_manager.SplitView(view.Object));
            _factory.Verify();
        }

        [Fact]
        public void SplitView3()
        {
            var view = _factory.Create<IWpfTextView>();
            var tuple = _adapter.MakeCodeWindowAndCommandTarget(view.Object, _factory);
            var codeWindow = tuple.Item1;
            var commandTarget = tuple.Item2;
            var id = VSConstants.GUID_VSStandardCommandSet97;
            commandTarget
                .Setup(x => x.Exec(ref id, It.IsAny<uint>(), It.IsAny<uint>(), IntPtr.Zero, IntPtr.Zero))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.True(_manager.SplitView(view.Object));
            _factory.Verify();
        }

        /// <summary>
        /// If there is no frame present then the call should fail
        /// </summary>
        [Fact]
        public void CloseView_NoWindowFrame()
        {
            var view = _factory.Create<IWpfTextView>().Object;
            Assert.False(_manager.CloseView(view));
        }

        /// <summary>
        /// If the frame is split then close should just remove the split
        /// </summary>
        [Fact]
        public void CloseView_Split()
        {
            var view = _factory.Create<IWpfTextView>().Object;
            var tuple = _adapter.MakeCodeWindowAndCommandTarget(view, _factory);
            tuple.Item1.MakeSplit(_adapter, _factory);
            var commandTarget = tuple.Item2;
            var id = VSConstants.GUID_VSStandardCommandSet97;
            commandTarget
                .Setup(x => x.Exec(ref id, It.IsAny<uint>(), It.IsAny<uint>(), IntPtr.Zero, IntPtr.Zero))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.True(_manager.CloseView(view));
            _factory.Verify();
        }

        /// <summary>
        /// The CloseView method shouldn't cause a save.  It should simply force close the
        /// ITextView
        /// </summary>
        [Fact]
        public void CloseView_DontSave()
        {
            var textView = _factory.Create<IWpfTextView>();
            var vsCodeWindow = _adapter.MakeCodeWindow(textView.Object, _factory);
            var vsWindowFrame = _adapter.MakeWindowFrame(textView.Object, _factory);
            vsWindowFrame
                .Setup(x => x.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.True(_manager.CloseView(textView.Object));
            _factory.Verify();
        }

        [Fact]
        public void MoveViewUp1()
        {
            var view = _factory.Create<IWpfTextView>().Object;
            Assert.False(_manager.MoveViewUp(view));
        }

        /// <summary>
        /// Secondary view on top, can't move up
        /// </summary>
        [Fact]
        public void MoveViewUp2()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            Assert.False(_manager.MoveViewUp(view2.Object));
        }

        [Fact]
        public void MoveViewUp3()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView2.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.E_FAIL).Verifiable();
            Assert.False(_manager.MoveViewUp(view1.Object));
            _factory.Verify();
        }

        [Fact]
        public void MoveViewUp4()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView2.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.S_OK).Verifiable();
            Assert.True(_manager.MoveViewUp(view1.Object));
            _factory.Verify();
        }

        [Fact]
        public void MoveViewDown1()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view2.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView1.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.E_FAIL).Verifiable();
            Assert.False(_manager.MoveViewDown(view2.Object));
            _factory.Verify();
        }

        [Fact]
        public void MoveViewDown2()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view2.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView1.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.S_OK).Verifiable();
            Assert.True(_manager.MoveViewDown(view2.Object));
            _factory.Verify();
        }
    }
}
