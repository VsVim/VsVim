using System;
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

namespace Vim.VisualStudio.UnitTest
{
    internal static class UnitTestExtensions
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
            adapter.Setup(x => x.GetCodeWindow(textView)).Returns(Result.CreateSuccess(mock.Object));
            return mock;
        }

        public static Tuple<Mock<IVsCodeWindow>, Mock<IOleCommandTarget>> MakeCodeWindowAndCommandTarget(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockRepository factory)
        {
            var mock1 = factory.Create<IVsCodeWindow>();
            var mock2 = mock1.As<IOleCommandTarget>();
            adapter.Setup(x => x.GetCodeWindow(textView)).Returns(Result.CreateSuccess(mock1.Object));
            return Tuple.Create(mock1, mock2);
        }

        public static Mock<IVsWindowFrame> MakeWindowFrame(
            this Mock<IVsAdapter> adapter,
            ITextView textView,
            MockRepository factory)
        {
            var mock = factory.Create<IVsWindowFrame>();
            adapter
                .Setup(x => x.GetContainingWindowFrame(textView))
                .Returns(Result.CreateSuccess(mock.Object));
            return mock;
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

        public static IReturnsResult<IOleCommandTarget> SetupExecOne(this Mock<IOleCommandTarget> mock, Guid? command = null, int hresult = VSConstants.S_OK)
        {
            var commandValue = command ?? VSConstants.VSStd2K;
            return mock.Setup(x => x.Exec(
                ref commandValue,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<IntPtr>(),
                It.IsAny<IntPtr>())).Returns(hresult);
        }

        public static void SetupExecAll(this Mock<IOleCommandTarget> mock, int hresult = VSConstants.S_OK)
        {
            var commandGroup = VSConstants.VSStd2K;
            mock.Setup(x => x.Exec(
                ref commandGroup,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<IntPtr>(),
                It.IsAny<IntPtr>())).Returns(hresult);

            commandGroup = VSConstants.GUID_VSStandardCommandSet97;
            mock.Setup(x => x.Exec(
                ref commandGroup,
                It.IsAny<uint>(),
                It.IsAny<uint>(),
                It.IsAny<IntPtr>(),
                It.IsAny<IntPtr>())).Returns(hresult);
        }

    }
}
