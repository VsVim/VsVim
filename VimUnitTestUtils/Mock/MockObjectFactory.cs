using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Vim.Extensions;

namespace Vim.UnitTest.Mock
{
    public static class MockObjectFactory
    {
        public static Mock<IIncrementalSearch> CreateIncrementalSearch(
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<IIncrementalSearch>();
        }

        public static Mock<ISearchService> CreateSearchService(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<ISearchService>();
        }

        public static Mock<IRegisterMap> CreateRegisterMap(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IRegisterMap>(MockBehavior.Strict);
            var reg = new Register(RegisterName.Unnamed);
            mock.Setup(x => x.GetRegister(RegisterName.Unnamed)).Returns(reg);
            return mock;
        }

        public static Mock<IClipboardDevice> CreateClipboardDevice(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var device = factory.Create<IClipboardDevice>();
            device.SetupProperty(x => x.Text);
            return device;
        }

        public static Mock<ITrackingLineColumnService> CreateTrackingLineColumnService()
        {
            var mock = new Mock<ITrackingLineColumnService>(MockBehavior.Strict);
            return mock;
        }

        public static Mock<IMacroRecorder> CreateMacroRecorder(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var recorder = factory.Create<IMacroRecorder>(MockBehavior.Loose);
            recorder.SetupGet(x => x.IsRecording).Returns(false);
            return recorder;
        }

        public static Mock<IVim> CreateVim(
            IRegisterMap registerMap = null,
            IMarkMap map = null,
            IVimGlobalSettings settings = null,
            IVimHost host = null,
            IKeyMap keyMap = null,
            IKeyboardDevice keyboardDevice = null,
            IMouseDevice mouseDevice = null,
            IVimData vimData = null,
            IMacroRecorder recorder = null,
            ISearchService searchService = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap(new TrackingLineColumnService());
            settings = settings ?? new GlobalSettings();
            host = host ?? new MockVimHost();
            keyMap = keyMap ?? (new KeyMap());
            recorder = recorder ?? CreateMacroRecorder(factory: factory).Object;
            searchService = searchService ?? factory.Create<ISearchService>().Object;
            keyboardDevice = keyboardDevice ?? (factory.Create<IKeyboardDevice>(MockBehavior.Loose)).Object;
            mouseDevice = mouseDevice ?? (factory.Create<IMouseDevice>(MockBehavior.Loose)).Object;
            vimData = vimData ?? new VimData();
            var mock = factory.Create<IVim>(MockBehavior.Strict);
            mock.SetupGet(x => x.RegisterMap).Returns(registerMap);
            mock.SetupGet(x => x.MarkMap).Returns(map);
            mock.SetupGet(x => x.Settings).Returns(settings);
            mock.SetupGet(x => x.VimHost).Returns(host);
            mock.SetupGet(x => x.KeyMap).Returns(keyMap);
            mock.SetupGet(x => x.VimData).Returns(vimData);
            mock.SetupGet(x => x.MacroRecorder).Returns(recorder);
            mock.SetupGet(x => x.SearchService).Returns(searchService);
            return mock;
        }

        public static Mock<IEditorOperations> CreateEditorOperations()
        {
            var mock = new Mock<IEditorOperations>(MockBehavior.Strict);
            return mock;
        }

        public static Mock<IVimGlobalSettings> CreateGlobalSettings(
            bool? ignoreCase = null,
            int? shiftWidth = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IVimGlobalSettings>(MockBehavior.Strict);
            if (ignoreCase.HasValue)
            {
                mock.SetupGet(x => x.IgnoreCase).Returns(ignoreCase.Value);
            }
            if (shiftWidth.HasValue)
            {
                mock.SetupGet(x => x.ShiftWidth).Returns(shiftWidth.Value);
            }

            mock.SetupGet(x => x.DisableCommand).Returns(GlobalSettings.DisableCommand);
            return mock;
        }

        public static Mock<IVimLocalSettings> CreateLocalSettings(
            IVimGlobalSettings global = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            global = global ?? CreateGlobalSettings(factory: factory).Object;
            var mock = factory.Create<IVimLocalSettings>(MockBehavior.Strict);
            mock.SetupGet(x => x.GlobalSettings).Returns(global);
            return mock;
        }

        public static Mock<IVimBuffer> CreateVimBuffer(
            ITextBuffer textBuffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var textView = CreateTextView(textBuffer: textBuffer, factory: factory);
            return CreateVimBuffer(textView: textView.Object, factory: factory);
        }

        public static Mock<IVimBuffer> CreateVimBuffer(
            ITextView textView,
            string name = null,
            IVim vim = null,
            IJumpList jumpList = null,
            IVimLocalSettings settings = null,
            IIncrementalSearch incrementalSearch = null,
            IMotionUtil motionUtil = null,
            ITextStructureNavigator wordNavigator = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            name = name ?? "test";
            vim = vim ?? CreateVim().Object;
            jumpList = jumpList ?? (factory.Create<IJumpList>().Object);
            motionUtil = motionUtil ?? factory.Create<IMotionUtil>().Object;
            wordNavigator = wordNavigator ?? factory.Create<ITextStructureNavigator>().Object;
            settings = settings ?? new LocalSettings(vim.Settings, FSharpOption<IEditorOptions>.None, FSharpOption.Create(textView));
            var mock = factory.Create<IVimBuffer>();
            mock.SetupGet(x => x.TextView).Returns(textView);
            mock.SetupGet(x => x.MotionUtil).Returns(motionUtil);
            mock.SetupGet(x => x.TextBuffer).Returns(() => textView.TextBuffer);
            mock.SetupGet(x => x.TextSnapshot).Returns(() => textView.TextSnapshot);
            mock.SetupGet(x => x.Name).Returns(name);
            mock.SetupGet(x => x.LocalSettings).Returns(settings);
            mock.SetupGet(x => x.MarkMap).Returns(vim.MarkMap);
            mock.SetupGet(x => x.RegisterMap).Returns(vim.RegisterMap);
            mock.SetupGet(x => x.JumpList).Returns(jumpList);
            mock.SetupGet(x => x.Vim).Returns(vim);
            mock.SetupGet(x => x.VimData).Returns(vim.VimData);
            mock.SetupGet(x => x.IncrementalSearch).Returns(incrementalSearch);
            mock.SetupGet(x => x.WordNavigator).Returns(wordNavigator);
            return mock;
        }

        public static Mock<ITextCaret> CreateCaret(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<ITextCaret>();
        }

        public static Mock<ITextSelection> CreateSelection(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<ITextSelection>();
        }

        public static Mock<ITextView> CreateTextView(
            ITextBuffer textBuffer = null,
            ITextCaret caret = null,
            ITextSelection selection = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            textBuffer = textBuffer ?? CreateTextBuffer(100, factory: factory).Object;
            caret = caret ?? CreateCaret(factory: factory).Object;
            selection = selection ?? CreateSelection(factory: factory).Object;
            var view = factory.Create<ITextView>();
            view.SetupGet(x => x.Caret).Returns(caret);
            view.SetupGet(x => x.Selection).Returns(selection);
            view.SetupGet(x => x.TextBuffer).Returns(textBuffer);
            view.SetupGet(x => x.TextSnapshot).Returns(() => textBuffer.CurrentSnapshot);
            return view;
        }

        public static Tuple<Mock<ITextView>, Mock<ITextCaret>, Mock<ITextSelection>> CreateTextViewAll(ITextBuffer buffer)
        {
            var caret = CreateCaret();
            var selection = CreateSelection();
            var view = CreateTextView(buffer, caret.Object, selection.Object);
            return Tuple.Create(view, caret, selection);
        }

        public static Tuple<Mock<ITextView>, MockRepository> CreateTextViewWithVisibleLines(
            ITextBuffer buffer,
            int startLine,
            int? endLine = null,
            int? caretPosition = null)
        {
            var factory = new MockRepository(MockBehavior.Strict);
            var endLineValue = endLine ?? startLine;
            var caretPositionValue = caretPosition ?? buffer.GetLine(startLine).Start.Position;
            var caret = factory.Create<ITextCaret>();
            caret.SetupGet(x => x.Position).Returns(
                new CaretPosition(
                    new VirtualSnapshotPoint(buffer.GetPoint(caretPositionValue)),
                    factory.Create<IMappingPoint>().Object,
                    PositionAffinity.Predecessor));

            var firstLine = factory.Create<ITextViewLine>();
            firstLine.SetupGet(x => x.Start).Returns(buffer.GetLine(startLine).Start);

            var lastLine = factory.Create<ITextViewLine>();
            lastLine.SetupGet(x => x.End).Returns(buffer.GetLine(endLineValue).End);

            var lines = factory.Create<ITextViewLineCollection>();
            lines.SetupGet(x => x.FirstVisibleLine).Returns(firstLine.Object);
            lines.SetupGet(x => x.LastVisibleLine).Returns(lastLine.Object);

            var visualBuffer = CreateTextBuffer(factory: factory);
            var textViewModel = factory.Create<ITextViewModel>();
            textViewModel.SetupGet(x => x.VisualBuffer).Returns(visualBuffer.Object);

            var properties = new PropertyCollection();
            var textView = factory.Create<ITextView>();
            var bufferGraph = factory.Create<IBufferGraph>();
            textView.SetupGet(x => x.TextBuffer).Returns(buffer);
            textView.SetupGet(x => x.TextViewLines).Returns(lines.Object);
            textView.SetupGet(x => x.Caret).Returns(caret.Object);
            textView.SetupGet(x => x.InLayout).Returns(false);
            textView.SetupGet(x => x.TextSnapshot).Returns(() => buffer.CurrentSnapshot);
            textView.SetupGet(x => x.Properties).Returns(properties);
            textView.SetupGet(x => x.BufferGraph).Returns(bufferGraph.Object);
            textView.SetupGet(x => x.TextViewModel).Returns(textViewModel.Object);
            textView.SetupGet(x => x.VisualSnapshot).Returns(visualBuffer.Object.CurrentSnapshot);
            return Tuple.Create(textView, factory);
        }

        public static Mock<ITextBuffer> CreateTextBuffer(int? snapshotLength = null, MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<ITextBuffer>();
            mock.SetupGet(x => x.Properties).Returns(new Microsoft.VisualStudio.Utilities.PropertyCollection());
            var snapshot = CreateTextSnapshot(snapshotLength ?? 0, mock.Object);
            mock.SetupGet(x => x.CurrentSnapshot).Returns(snapshot.Object);
            return mock;
        }

        public static Mock<ITextVersion> CreateTextVersion(int? versionNumber = null)
        {
            var number = versionNumber ?? 1;
            var mock = new Mock<ITextVersion>(MockBehavior.Strict);
            mock.SetupGet(x => x.VersionNumber).Returns(number);
            return mock;
        }

        public static Mock<ITextSnapshot> CreateTextSnapshot(
            int length,
            ITextBuffer buffer = null,
            int? versionNumber = null)
        {

            buffer = buffer ?? CreateTextBuffer().Object;
            var mock = new Mock<ITextSnapshot>(MockBehavior.Strict);
            mock.SetupGet(x => x.Length).Returns(length);
            mock.SetupGet(x => x.TextBuffer).Returns(buffer);
            mock.SetupGet(x => x.Version).Returns(CreateTextVersion(versionNumber).Object);
            return mock;
        }

        public static SnapshotPoint CreateSnapshotPoint(int position)
        {
            var snapshot = CreateTextSnapshot(position + 1);
            snapshot.Setup(x => x.GetText(It.IsAny<int>(), It.IsAny<int>())).Returns("Mocked ToString()");
            return new SnapshotPoint(snapshot.Object, position);
        }

        public static Mock<IServiceProvider> CreateServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            return CreateServiceProvider(null, serviceList);
        }

        /// <summary>
        /// Create an IServiceProvider instance which provides services of the specified object via 
        /// the provided Type
        /// </summary>
        public static Mock<IServiceProvider> CreateServiceProvider(MockRepository factory, params Tuple<Type, object>[] serviceList)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IServiceProvider>();
            foreach (var tuple in serviceList)
            {
                var localTuple = tuple;
                mock.Setup(x => x.GetService(localTuple.Item1)).Returns(localTuple.Item2);
            }

            return mock;
        }

        public static Mock<SVsServiceProvider> CreateVsServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            return CreateVsServiceProvider(null, serviceList);
        }

        public static Mock<SVsServiceProvider> CreateVsServiceProvider(MockRepository factory, params Tuple<Type, object>[] serviceList)
        {
            var mock = CreateServiceProvider(factory, serviceList);
            return mock.As<SVsServiceProvider>();
        }

        public static IEnumerable<Mock<EnvDTE.Command>> CreateCommandList(params string[] args)
        {
            foreach (var binding in args)
            {
                var localBinding = binding;
                var mock = new Mock<EnvDTE.Command>(MockBehavior.Strict);
                mock.Setup(x => x.Bindings).Returns(localBinding);
                mock.Setup(x => x.Name).Returns("example command");
                mock.Setup(x => x.LocalizedName).Returns("example command");
                yield return mock;
            }
        }

        public static Mock<EnvDTE.Commands> CreateCommands(IEnumerable<EnvDTE.Command> commands)
        {
            var mock = new Mock<EnvDTE.Commands>(MockBehavior.Strict);
            var enumMock = mock.As<IEnumerable>();
            mock.Setup(x => x.GetEnumerator()).Returns(commands.GetEnumerator());
            enumMock.Setup(x => x.GetEnumerator()).Returns(commands.GetEnumerator());
            return mock;
        }

        public static Mock<_DTE> CreateDteWithCommands(params string[] args)
        {
            var commandList = CreateCommandList(args).Select(x => x.Object);
            var commands = CreateCommands(commandList);
            var dte = new Mock<_DTE>();
            dte.SetupGet(x => x.Commands).Returns(commands.Object);
            return dte;
        }

        public static Mock<IMappingSpan> CreateMappingSpan(SnapshotSpan span, MockRepository factory = null)
        {
            return CreateMappingSpan(new[] { span }, factory);
        }

        public static Mock<IMappingSpan> CreateMappingSpan(SnapshotSpan[] spans, MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var col = new NormalizedSnapshotSpanCollection(spans);
            var mock = factory.Create<IMappingSpan>();
            mock.Setup(x => x.GetSpans(spans[0].Snapshot)).Returns(col);
            mock.Setup(x => x.GetSpans(spans[0].Snapshot.TextBuffer)).Returns(col);
            return mock;
        }

        public static Mock<IMappingTagSpan<ITag>> CreateMappingTagSpan(
            SnapshotSpan span,
            ITag tag = null,
            MockRepository factory = null)
        {
            return CreateMappingTagSpan(
                CreateMappingSpan(span, factory).Object,
                tag,
                factory);
        }

        public static Mock<IMappingTagSpan<ITag>> CreateMappingTagSpan(
            IMappingSpan span,
            ITag tag = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IMappingTagSpan<ITag>>();
            mock.SetupGet(x => x.Span).Returns(span);
            if (tag != null)
            {
                mock.SetupGet(x => x.Tag).Returns(tag);
            }
            return mock;
        }


        public static Mock<IVsTextLineMarker> CreateVsTextLineMarker(
            TextSpan span,
            MARKERTYPE type,
            MockRepository factory = null)
        {
            return CreateVsTextLineMarker(span, (int)type, factory);
        }

        public static Mock<IVsTextLineMarker> CreateVsTextLineMarker(
            TextSpan span,
            int type,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IVsTextLineMarker>();
            mock.Setup(x => x.GetType(out type)).Returns(VSConstants.S_OK);
            mock
                .Setup(x => x.GetCurrentSpan(It.IsAny<TextSpan[]>()))
                .Callback<TextSpan[]>(x => { x[0] = span; })
                .Returns(VSConstants.S_OK);
            return mock;
        }

        public static Mock<ITextViewLine> CreateTextViewLine(
            ITextSnapshotLine textLine,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<ITextViewLine>();
            mock.SetupGet(x => x.Start).Returns(textLine.Start);
            mock.SetupGet(x => x.End).Returns(textLine.End);
            mock.SetupGet(x => x.EndIncludingLineBreak).Returns(textLine.EndIncludingLineBreak);
            return mock;
        }

        public static Mock<ITextViewLineCollection> CreateTextViewLineCollection(
            SnapshotLineRange range,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<ITextViewLineCollection>();
            for (int i = 0; i < range.Count; i++)
            {
                var number = range.StartLineNumber + i;
                var line = range.Snapshot.GetLineFromLineNumber(number);
                var localIndex = i;
                mock.Setup(x => x[localIndex]).Returns(CreateTextViewLine(line, factory).Object);
            }

            mock.SetupGet(x => x.FirstVisibleLine).Returns(CreateTextViewLine(range.StartLine, factory).Object);
            mock.SetupGet(x => x.LastVisibleLine).Returns(CreateTextViewLine(range.EndLine, factory).Object);
            mock.SetupGet(x => x.Count).Returns(range.Count);
            return mock;
        }
    }
}
