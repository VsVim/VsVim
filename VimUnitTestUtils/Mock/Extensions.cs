using System;
using System.Linq;
using System.Windows;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim.Extensions;
using VsVim;

namespace Vim.UnitTest.Mock
{
    public static class VsVimTestExtensions
    {
        public static void MakeSplit(
            this Mock<IVsCodeWindow> mock,
            Mock<IVsAdapter> adapter,
            MockRepository factory)
        {
            MakePrimaryView(mock, adapter, factory.Create<IWpfTextView>().Object, factory);
            MakeSecondaryView(mock, adapter, factory.Create<IWpfTextView>().Object, factory);
        }

        public static Mock<IVsTextView> MakeVsTextView(
            this IWpfTextView textView,
            Mock<IVsAdapter> adapter,
            MockRepository factory)
        {
            var vsView = factory.Create<IVsTextView>();
            var editorAdapter = global::Moq.Mock.Get<IVsEditorAdaptersFactoryService>(adapter.Object.EditorAdapter);
            editorAdapter.Setup(x => x.GetViewAdapter(textView)).Returns(vsView.Object);
            editorAdapter.Setup(x => x.GetWpfTextView(vsView.Object)).Returns(textView);
            return vsView;
        }

        public static Mock<IVsTextView> MakePrimaryView(
            this Mock<IVsCodeWindow> window,
            Mock<IVsAdapter> adapter,
            IWpfTextView textView,
            MockRepository factory)
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
            MockRepository factory)
        {
            factory = factory ?? new MockRepository(MockBehavior.Loose);
            var vsViewMock = MakeVsTextView(textView, adapter, factory);
            var vsView = vsViewMock.Object;
            window.Setup(x => x.GetSecondaryView(out vsView)).Returns(VSConstants.S_OK);
            return vsViewMock;
        }

        public static Mock<IVsCodeWindow> MakeCodeWindow(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockRepository factory)
        {
            factory = factory ?? new MockRepository(MockBehavior.Loose);
            var mock = factory.Create<IVsCodeWindow>();
            var obj = mock.Object;
            adapter.Setup(x => x.TryGetCodeWindow(textView, out obj)).Returns(true);
            return mock;
        }

        public static Tuple<Mock<IVsCodeWindow>, Mock<IOleCommandTarget>> MakeCodeWindowAndCommandTarget(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockRepository factory)
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
            MockRepository factory)
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
            MockRepository factory)
        {
            factory = factory ?? new MockRepository(MockBehavior.Loose);
            var element = factory.Create<FrameworkElement>();
            textView.SetupGet(x => x.VisualElement).Returns(element.Object);
            return element;
        }

        public static void MakeLastCharSearch(
            this Mock<IMotionCaptureGlobalData> mock,
            Action<int> forward,
            Action<int> backward)
        {
            var forwardFunc = FuncUtil.CreateMotionFunc(forward);
            var backwardFunc = FuncUtil.CreateMotionFunc(backward);
            var value = FSharpOption.Create(Tuple.Create(forwardFunc, backwardFunc));
            mock.SetupGet(x => x.LastCharSearch).Returns(value);
        }

        public static void MakeLastCharSearch(
            this Mock<IMotionCaptureGlobalData> mock,
            Func<int, MotionData> forward,
            Func<int, MotionData> backward)
        {
            var forwardFunc = FuncUtil.CreateMotionFunc(forward);
            var backwardFunc = FuncUtil.CreateMotionFunc(backward);
            var value = FSharpOption.Create(Tuple.Create(forwardFunc, backwardFunc));
            mock.SetupGet(x => x.LastCharSearch).Returns(value);
        }

        public static void MakeLastCharSearchNone(this Mock<IMotionCaptureGlobalData> mock)
        {
            var value = FSharpOption<Tuple<FSharpFunc<MotionUse, FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>>>, FSharpFunc<MotionUse, FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>>>>>.None;
            mock.SetupGet(x => x.LastCharSearch).Returns(value);
        }

        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            VirtualSnapshotSpan span)
        {
            selection.Setup(x => x.Mode).Returns(TextSelectionMode.Stream);
            selection.Setup(x => x.StreamSelectionSpan).Returns(span);
        }

        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            NormalizedSnapshotSpanCollection col)
        {
            selection.Setup(x => x.Mode).Returns(TextSelectionMode.Box);
            selection.Setup(x => x.SelectedSpans).Returns(col);
            var start = col.Min(x => x.Start);
            var end = col.Min(x => x.End);
            selection
                .Setup(x => x.StreamSelectionSpan)
                .Returns(new VirtualSnapshotSpan(new SnapshotSpan(start, end)));
        }


        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            params SnapshotSpan[] spans)
        {
            if (spans.Length == 1)
            {
                MakeSelection(selection, new VirtualSnapshotSpan(spans[0]));
            }
            else
            {
                MakeSelection(selection, new NormalizedSnapshotSpanCollection(spans));
            }
        }

        public static void MakeUndoRedoPossible(
            this Mock<IUndoRedoOperations> mock,
            MockRepository factory)
        {
            mock
                .Setup(x => x.CreateUndoTransaction(It.IsAny<string>()))
                .Returns(() => factory.Create<IUndoTransaction>(MockBehavior.Loose).Object);
        }

        public static void AddMark(
            this Mock<IMarkMap> map,
            ITextBuffer buffer,
            char mark,
            VirtualSnapshotPoint? point = null)
        {
            if (point.HasValue)
            {
                map.Setup(x => x.GetMark(buffer, mark)).Returns(FSharpOption.Create(point.Value));
            }
            else
            {
                map.Setup(x => x.GetMark(buffer, mark)).Returns(FSharpOption<VirtualSnapshotPoint>.None);
            }
        }

        public static void AddMark(
            this Mock<IMarkMap> map,
            ITextBuffer buffer,
            char mark,
            SnapshotPoint? point = null)
        {
            VirtualSnapshotPoint? virtualPoint = null;
            if (point.HasValue)
            {
                virtualPoint = new VirtualSnapshotPoint(point.Value);
            }
            AddMark(map, buffer, mark, virtualPoint);
        }
    }
}
