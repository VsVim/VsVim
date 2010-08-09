using System;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using VsVim;

namespace VsVimTest
{
    public static class VsVimTestExtensions
    {
        public static void MakeSplit(
            this Mock<IVsCodeWindow> mock,
            Mock<IVsAdapter> adapter,
            MockFactory factory)
        {
            MakePrimaryView(mock, adapter, factory.Create<IWpfTextView>().Object, factory);
            MakeSecondaryView(mock, adapter, factory.Create<IWpfTextView>().Object, factory);
        }

        public static Mock<IVsTextView> MakeVsTextView(
            this IWpfTextView textView,
            Mock<IVsAdapter> adapter,
            MockFactory factory)
        {
            var vsView = factory.Create<IVsTextView>();
            var editorAdapter = Mock.Get<IVsEditorAdaptersFactoryService>(adapter.Object.EditorAdapter);
            editorAdapter.Setup(x => x.GetViewAdapter(textView)).Returns(vsView.Object);
            editorAdapter.Setup(x => x.GetWpfTextView(vsView.Object)).Returns(textView);
            return vsView;
        }

        public static Mock<IVsTextView> MakePrimaryView(
            this Mock<IVsCodeWindow> window,
            Mock<IVsAdapter> adapter,
            IWpfTextView textView,
            MockFactory factory)
        {
            var vsViewMock = MakeVsTextView(textView, adapter, factory);
            var vsView = vsViewMock.Object;
            window.Setup(x => x.GetPrimaryView(out vsView)).Returns(VSConstants.S_OK);
            return vsViewMock;
        }

        public static Mock<IVsTextView> MakeSecondaryView(
            this Mock<IVsCodeWindow> window,
            Mock<IVsAdapter> adapter,
            IWpfTextView textView,
            MockFactory factory)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var vsViewMock = MakeVsTextView(textView, adapter, factory);
            var vsView = vsViewMock.Object;
            window.Setup(x => x.GetSecondaryView(out vsView)).Returns(VSConstants.S_OK);
            return vsViewMock;
        }

        public static Mock<IVsCodeWindow> MakeCodeWindow(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var mock = factory.Create<IVsCodeWindow>();
            var obj = mock.Object;
            adapter.Setup(x => x.TryGetCodeWindow(textView, out obj)).Returns(true);
            return mock;
        }

        public static Tuple<Mock<IVsCodeWindow>, Mock<IOleCommandTarget>> MakeCodeWindowAndCommandTarget(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory)
        {
            var mock1 = factory.Create<IVsCodeWindow>();
            var mock2 = mock1.As<IOleCommandTarget>();
            var obj = mock1.Object;
            adapter.Setup(x => x.TryGetCodeWindow(textView, out obj)).Returns(true);
            return Tuple.Create(mock1, mock2);
        }

        public static Mock<IVsWindowFrame> MakeWindowFrame(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockFactory factory)
        {
            var mock = factory.Create<IVsWindowFrame>();
            IVsWindowFrame frame = mock.Object;
            adapter
                .Setup(x => x.TryGetContainingWindowFrame(textView, out frame))
                .Returns(true);
            return mock;
        }

        public static Mock<FrameworkElement> MakeVisualElement(
            this Mock<IWpfTextView> textView,
            MockFactory factory)
        {
            factory = factory ?? new MockFactory(MockBehavior.Loose);
            var element = factory.Create<FrameworkElement>();
            textView.SetupGet(x => x.VisualElement).Returns(element.Object);
            return element;
        }

    }
}
