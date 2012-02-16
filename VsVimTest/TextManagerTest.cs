using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using VsVim.Implementation;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class TextManagerTest
    {
        private MockRepository _factory;
        private Mock<IVsAdapter> _adapter;
        private Mock<SVsServiceProvider> _serviceProvider;
        private Mock<IVsRunningDocumentTable> _table;
        private TextManager _managerRaw;
        private ITextManager _manager;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _adapter = _factory.Create<IVsAdapter>();
            _adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
            _table = _factory.Create<IVsRunningDocumentTable>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider
                .Setup(x => x.GetService(typeof(SVsRunningDocumentTable)))
                .Returns(_table.Object);
            _managerRaw = new TextManager(
                _adapter.Object,
                _factory.Create<ITextDocumentFactoryService>().Object,
                _serviceProvider.Object);
            _manager = _managerRaw;
        }

        [Test]
        public void SplitView1()
        {
            var view = _factory.Create<IWpfTextView>();
            Assert.IsFalse(_manager.SplitView(view.Object));
        }

        [Test]
        public void SplitView2()
        {
            var view = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view.Object, _factory);
            Assert.IsFalse(_manager.SplitView(view.Object));
            _factory.Verify();
        }

        [Test]
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
            Assert.IsTrue(_manager.SplitView(view.Object));
            _factory.Verify();
        }

        /// <summary>
        /// If there is no frame present then the call should fail
        /// </summary>
        [Test]
        public void CloseView_NoWindowFrame()
        {
            var view = _factory.Create<IWpfTextView>().Object;
            Assert.IsFalse(_manager.CloseView(view));
        }

        /// <summary>
        /// If the frame is split then close should just remove the split
        /// </summary>
        [Test]
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
            Assert.IsTrue(_manager.CloseView(view));
            _factory.Verify();
        }

        /// <summary>
        /// The CloseView method shouldn't cause a save.  It should simply force close the
        /// ITextView
        /// </summary>
        [Test]
        public void CloseView_DontSave()
        {
            var textView = _factory.Create<IWpfTextView>();
            var vsCodeWindow = _adapter.MakeCodeWindow(textView.Object, _factory);
            var vsWindowFrame = _adapter.MakeWindowFrame(textView.Object, _factory);
            vsWindowFrame
                .Setup(x => x.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave))
                .Returns(VSConstants.S_OK)
                .Verifiable();
            Assert.IsTrue(_manager.CloseView(textView.Object));
            _factory.Verify();
        }

        [Test]
        public void MoveViewUp1()
        {
            var view = _factory.Create<IWpfTextView>().Object;
            Assert.IsFalse(_manager.MoveViewUp(view));
        }

        [Test]
        [Description("Secondary view on top, can't move up")]
        public void MoveViewUp2()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            Assert.IsFalse(_manager.MoveViewUp(view2.Object));
        }

        [Test]
        public void MoveViewUp3()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView2.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.E_FAIL).Verifiable();
            Assert.IsFalse(_manager.MoveViewUp(view1.Object));
            _factory.Verify();
        }

        [Test]
        public void MoveViewUp4()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view1.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView2.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.S_OK).Verifiable();
            Assert.IsTrue(_manager.MoveViewUp(view1.Object));
            _factory.Verify();
        }

        [Test]
        public void MoveViewDown1()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view2.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView1.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.E_FAIL).Verifiable();
            Assert.IsFalse(_manager.MoveViewDown(view2.Object));
            _factory.Verify();
        }

        [Test]
        public void MoveViewDown2()
        {
            var view1 = _factory.Create<IWpfTextView>();
            var view2 = _factory.Create<IWpfTextView>();
            var codeWindow = _adapter.MakeCodeWindow(view2.Object, _factory);
            var vsView1 = codeWindow.MakePrimaryView(_adapter, view1.Object, _factory);
            var vsView2 = codeWindow.MakeSecondaryView(_adapter, view2.Object, _factory);
            vsView1.Setup(x => x.SendExplicitFocus()).Returns(VSConstants.S_OK).Verifiable();
            Assert.IsTrue(_manager.MoveViewDown(view2.Object));
            _factory.Verify();
        }
    }
}
