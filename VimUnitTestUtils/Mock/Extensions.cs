using System;
using System.Linq;
using System.Windows;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Moq.Language.Flow;
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
            ITextBuffer textBuffer,
            MockRepository factory)
        {
            var mock = factory.Create<IVsWindowFrame>();
            adapter
                .Setup(x => x.GetContainingWindowFrame(textBuffer))
                .Returns(Result.CreateSuccess(mock.Object));
            return mock;
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

        public static Mock<IEditorOptions> MakeOptions(
            this Mock<IEditorOptionsFactoryService> optionsFactory,
            ITextBuffer buffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var options = factory.Create<IEditorOptions>();
            optionsFactory
                .Setup(x => x.GetOptions(buffer))
                .Returns(options.Object);
            return options;
        }

        public static Mock<IVsTextBuffer> MakeBufferAdapter(
            this Mock<IVsEditorAdaptersFactoryService> adapterFactory,
            ITextBuffer textBuffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var adapter = factory.Create<IVsTextBuffer>();
            adapterFactory.Setup(x => x.GetBufferAdapter(textBuffer)).Returns(adapter.Object);
            return adapter;
        }

        public static Mock<T> MakeService<T>(
            this Mock<System.IServiceProvider> serviceProvider,
            MockRepository factory = null) where T : class
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var service = factory.Create<T>();
            serviceProvider.Setup(x => x.GetService(typeof(T))).Returns(service.Object);
            return service;
        }

        public static Mock<TInterface> MakeService<TService, TInterface>(
            this Mock<SVsServiceProvider> serviceProvider,
            MockRepository factory = null) where TInterface : class
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var service = factory.Create<TInterface>();
            serviceProvider.Setup(x => x.GetService(typeof(TService))).Returns(service.Object);
            return service;
        }

        public static void SetupNoEnumMarkers(this Mock<IVsTextLines> mock)
        {
            IVsEnumLineMarkers markers;
            mock
                .Setup(x => x.EnumMarkers(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<uint>(),
                    out markers))
                .Returns(VSConstants.E_FAIL);
        }

        public static void SetupEnumMarkers(this Mock<IVsTextLines> mock, IVsEnumLineMarkers markers)
        {
            var local = markers;
            mock
                .Setup(x => x.EnumMarkers(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<uint>(),
                    out local))
                .Returns(VSConstants.S_OK);
        }

        public static IReturnsResult<IOleCommandTarget> SetupQueryStatus(
            this Mock<IOleCommandTarget> mock,
            Guid? command = null,
            int hresult = VSConstants.S_OK)
        {
            var commandValue = command ?? VSConstants.VSStd2K;
            return mock.Setup(x => x.QueryStatus(
                ref commandValue,
                It.IsAny<uint>(),
                It.IsAny<OLECMD[]>(),
                It.IsAny<IntPtr>())).Returns(hresult);
        }

        public static IReturnsResult<IOleCommandTarget> SetupExec(this Mock<IOleCommandTarget> mock, Guid? command = null, int hresult = VSConstants.S_OK)
        {
            var commandValue = command ?? VSConstants.VSStd2K;
            return mock.Setup(x => x.Exec(
                ref commandValue,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<IntPtr>(),
                It.IsAny<IntPtr>())).Returns(hresult);
        }

        public static void SetupNormalCommand(this Mock<ICommandUtil> commandUtil, NormalCommand command, int? count = null, RegisterName registerName = null)
        {
            var realCount = count.HasValue ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            var realName = registerName != null ? FSharpOption.Create(registerName) : FSharpOption<RegisterName>.None;
            var commandData = new CommandData(realCount, realName);
            commandUtil.Setup(x => x.RunNormalCommand(command, commandData));
        }
    }
}
