using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using VsVim;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;

namespace VsVimTest
{
    internal static class VsVimTestExtensions
    {
        internal static void MakeSplit(
            this Mock<IVsCodeWindow> mock,
            Mock<IVsAdapter> adapter,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            MakePrimaryView(mock, adapter, factory.Create<ITextView>().Object, factory);
            MakeSecondaryView(mock, adapter, factory.Create<ITextView>().Object, factory);
        }

        internal static Mock<IVsTextView> MakePrimaryView(
            this Mock<IVsCodeWindow> window,
            Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var vsViewMock = factory.Create<IVsTextView>();
            var editorMock = Mock.Get<IVsEditorAdaptersFactoryService>(adapter.Object.EditorAdapter);
            editorMock.Setup(x => x.GetViewAdapter(textView)).Returns(vsViewMock.Object);
            var vsView = vsViewMock.Object;
            window.Setup(x => x.GetPrimaryView(out vsView)).Returns(VSConstants.S_OK);
            return vsViewMock;
        }

        internal static Mock<IVsTextView> MakeSecondaryView(
            this Mock<IVsCodeWindow> window,
            Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var vsViewMock = factory.Create<IVsTextView>();
            var editorMock = Mock.Get<IVsEditorAdaptersFactoryService>(adapter.Object.EditorAdapter);
            editorMock.Setup(x => x.GetViewAdapter(textView)).Returns(vsViewMock.Object);
            var vsView = vsViewMock.Object;
            window.Setup(x => x.GetSecondaryView(out vsView)).Returns(VSConstants.S_OK);
            return vsViewMock;
        }

        internal static Mock<IVsCodeWindow> MakeCodeWindow(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var mock = factory.Create<IVsCodeWindow>();
            var obj = mock.Object;
            adapter.Setup(x => x.TryGetCodeWindow(textView, out obj)).Returns(true);
            return mock;
        }

        internal static Tuple<Mock<IVsCodeWindow>,Mock<IOleCommandTarget>> MakeCodeWindowAndCommandTarget(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory = null)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var mock1 = factory.Create<IVsCodeWindow>();
            var mock2 = mock1.As<IOleCommandTarget>();
            var obj = mock1.Object;
            adapter.Setup(x => x.TryGetCodeWindow(textView, out obj)).Returns(true);
            return Tuple.Create(mock1, mock2);
        }
    }
}
