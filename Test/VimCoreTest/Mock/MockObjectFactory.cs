using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Moq;
using System.Collections.Generic;
using Vim.Interpreter;
using System.Windows.Media;

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
            IMacroRecorder macroRecorder = null,
            ISearchService searchService = null,
            Dictionary<string, VariableValue> variableMap = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap(new BufferTrackingService());
            settings = settings ?? new GlobalSettings();
            host = host ?? new MockVimHost();
            keyMap = keyMap ?? (new KeyMap(settings, new Dictionary<string, VariableValue>()));
            macroRecorder = macroRecorder ?? CreateMacroRecorder(factory: factory).Object;
            searchService = searchService ?? factory.Create<ISearchService>().Object;
            keyboardDevice = keyboardDevice ?? (factory.Create<IKeyboardDevice>(MockBehavior.Loose)).Object;
            mouseDevice = mouseDevice ?? (factory.Create<IMouseDevice>(MockBehavior.Loose)).Object;
            vimData = vimData ?? VimUtil.CreateVimData();
            variableMap = variableMap ?? new Dictionary<string, VariableValue>();
            var mock = factory.Create<IVim>(MockBehavior.Strict);
            mock.SetupGet(x => x.RegisterMap).Returns(registerMap);
            mock.SetupGet(x => x.MarkMap).Returns(map);
            mock.SetupGet(x => x.GlobalSettings).Returns(settings);
            mock.SetupGet(x => x.VimHost).Returns(host);
            mock.SetupGet(x => x.KeyMap).Returns(keyMap);
            mock.SetupGet(x => x.VimData).Returns(vimData);
            mock.SetupGet(x => x.MacroRecorder).Returns(macroRecorder);
            mock.SetupGet(x => x.SearchService).Returns(searchService);
            mock.SetupGet(x => x.VariableMap).Returns(variableMap);
            return mock;
        }

        public static Mock<IEditorOperations> CreateEditorOperations()
        {
            var mock = new Mock<IEditorOperations>(MockBehavior.Strict);
            return mock;
        }

        public static Mock<IVimGlobalSettings> CreateGlobalSettings(
            bool? ignoreCase = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IVimGlobalSettings>(MockBehavior.Strict);
            if (ignoreCase.HasValue)
            {
                mock.SetupGet(x => x.IgnoreCase).Returns(ignoreCase.Value);
            }

            mock.SetupGet(x => x.DisableAllCommand).Returns(GlobalSettings.DisableAllCommand);
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
            IVimLocalSettings localSettings = null,
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
            localSettings = localSettings ?? new LocalSettings(vim.GlobalSettings);
            var vimTextBuffer = CreateVimTextBuffer(
                textView.TextBuffer,
                localSettings: localSettings,
                vim: vim,
                wordNavigator: wordNavigator,
                factory: factory);
            var mock = factory.Create<IVimBuffer>();
            mock.SetupGet(x => x.TextView).Returns(textView);
            mock.SetupGet(x => x.MotionUtil).Returns(motionUtil);
            mock.SetupGet(x => x.TextBuffer).Returns(() => textView.TextBuffer);
            mock.SetupGet(x => x.TextSnapshot).Returns(() => textView.TextSnapshot);
            mock.SetupGet(x => x.Name).Returns(name);
            mock.SetupGet(x => x.LocalSettings).Returns(localSettings);
            mock.SetupGet(x => x.GlobalSettings).Returns(localSettings.GlobalSettings);
            mock.SetupGet(x => x.MarkMap).Returns(vim.MarkMap);
            mock.SetupGet(x => x.RegisterMap).Returns(vim.RegisterMap);
            mock.SetupGet(x => x.JumpList).Returns(jumpList);
            mock.SetupGet(x => x.Vim).Returns(vim);
            mock.SetupGet(x => x.VimData).Returns(vim.VimData);
            mock.SetupGet(x => x.IncrementalSearch).Returns(incrementalSearch);
            mock.SetupGet(x => x.WordNavigator).Returns(wordNavigator);
            mock.SetupGet(x => x.VimTextBuffer).Returns(vimTextBuffer.Object);
            return mock;
        }

        /// <summary>
        /// Create a Mock over IVimTextBuffer which provides the msot basic functions
        /// </summary>
        public static Mock<IVimTextBuffer> CreateVimTextBuffer(
            ITextBuffer textBuffer,
            IVimLocalSettings localSettings = null,
            IVim vim = null,
            ITextStructureNavigator wordNavigator = null,
            IUndoRedoOperations undoRedoOperations = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            vim = vim ?? CreateVim(factory: factory).Object;
            localSettings = localSettings ?? CreateLocalSettings(factory: factory).Object;
            wordNavigator = wordNavigator ?? factory.Create<ITextStructureNavigator>().Object;
            undoRedoOperations = undoRedoOperations ?? factory.Create<IUndoRedoOperations>().Object;
            var mock = factory.Create<IVimTextBuffer>();
            mock.SetupGet(x => x.TextBuffer).Returns(textBuffer);
            mock.SetupGet(x => x.LocalSettings).Returns(localSettings);
            mock.SetupGet(x => x.GlobalSettings).Returns(localSettings.GlobalSettings);
            mock.SetupGet(x => x.Vim).Returns(vim);
            mock.SetupGet(x => x.WordNavigator).Returns(wordNavigator);
            mock.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            mock.SetupGet(x => x.UndoRedoOperations).Returns(undoRedoOperations);
            mock.SetupProperty(x => x.LastVisualSelection);
            mock.SetupProperty(x => x.LastInsertExitPoint);
            mock.SetupProperty(x => x.LastEditPoint);
            mock.Setup(x => x.SwitchMode(It.IsAny<ModeKind>(), It.IsAny<ModeArgument>()));
            return mock;
        }

        /// <summary>
        /// This is not technically a Mock but often we want to create it with mocked 
        /// backing values
        /// </summary>
        /// <returns></returns>
        public static IVimBufferData CreateVimBufferData(
            IVimTextBuffer vimTextBuffer,
            ITextView textView,
            IJumpList jumpList = null,
            IStatusUtil statusUtil = null,
            IVimWindowSettings windowSettings = null,
            IWordUtil wordUtil = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            statusUtil = statusUtil ?? factory.Create<IStatusUtil>().Object;
            jumpList = jumpList ?? factory.Create<IJumpList>().Object;
            wordUtil = wordUtil ?? factory.Create<IWordUtil>().Object;
            windowSettings = windowSettings ?? factory.Create<IVimWindowSettings>().Object;
            return new VimBufferData(
                vimTextBuffer,
                textView,
                windowSettings,
                jumpList,
                statusUtil,
                wordUtil);
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

        public static Mock<ITextViewRoleSet> CreateTextViewRoleSet(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var textViewRoleSet = factory.Create<ITextViewRoleSet>();
            textViewRoleSet.Setup(x => x.Contains(It.IsAny<string>())).Returns(false);
            return textViewRoleSet;
        }

        public static Mock<IEditorOptions> CreateEditorOptions(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IEditorOptions>();
            return mock;
        }

        public static Mock<IBufferGraph> CreateBufferGraph(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<IBufferGraph>();
        }

        public static Mock<ITextViewModel> CreateTextViewModel(
            ITextBuffer textBuffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var textViewModel = factory.Create<ITextViewModel>();
            textViewModel.SetupGet(x => x.DataBuffer).Returns(textBuffer);
            textViewModel.SetupGet(x => x.VisualBuffer).Returns(textBuffer);
            textViewModel.SetupGet(x => x.EditBuffer).Returns(textBuffer);
            return textViewModel;
        }

        public static Mock<ITextView> CreateTextView(
            ITextBuffer textBuffer = null,
            ITextCaret caret = null,
            ITextSelection selection = null,
            ITextViewRoleSet textViewRoleSet = null,
            ITextViewModel textViewModel = null,
            IEditorOptions editorOptions = null,
            IBufferGraph bufferGraph = null,
            ITextDataModel textDataModel = null,
            PropertyCollection propertyCollection = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            textBuffer = textBuffer ?? CreateTextBuffer(100, factory: factory).Object;
            caret = caret ?? CreateCaret(factory: factory).Object;
            selection = selection ?? CreateSelection(factory: factory).Object;
            propertyCollection = propertyCollection ?? new PropertyCollection();
            textViewRoleSet = textViewRoleSet ?? CreateTextViewRoleSet(factory: factory).Object;
            editorOptions = editorOptions ?? CreateEditorOptions(factory: factory).Object;
            bufferGraph = bufferGraph ?? CreateBufferGraph(factory: factory).Object;
            textViewModel = textViewModel ?? CreateTextViewModel(textBuffer: textBuffer, factory: factory).Object;
            textDataModel = textDataModel ?? CreateTextDataModel(textBuffer: textBuffer, factory: factory).Object;
            var textView = factory.Create<ITextView>();
            textView.SetupGet(x => x.Caret).Returns(caret);
            textView.SetupGet(x => x.Selection).Returns(selection);
            textView.SetupGet(x => x.TextBuffer).Returns(textBuffer);
            textView.SetupGet(x => x.TextSnapshot).Returns(() => textBuffer.CurrentSnapshot);
            textView.SetupGet(x => x.Properties).Returns(propertyCollection);
            textView.SetupGet(x => x.Roles).Returns(textViewRoleSet);
            textView.SetupGet(x => x.Options).Returns(editorOptions);
            textView.SetupGet(x => x.BufferGraph).Returns(bufferGraph);
            textView.SetupGet(x => x.TextViewModel).Returns(textViewModel);
            textView.SetupGet(x => x.TextDataModel).Returns(textDataModel);
            return textView;
        }

        public static Tuple<Mock<ITextView>, Mock<ITextCaret>, Mock<ITextSelection>> CreateTextViewAll(ITextBuffer buffer)
        {
            var caret = CreateCaret();
            var selection = CreateSelection();
            var view = CreateTextView(buffer, caret.Object, selection.Object);
            return Tuple.Create(view, caret, selection);
        }

        public static Mock<ITextDataModel> CreateTextDataModel(
            ITextBuffer textBuffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<ITextDataModel>();
            mock.SetupGet(x => x.DataBuffer).Returns(textBuffer);
            mock.SetupGet(x => x.DocumentBuffer).Returns(textBuffer);
            return mock;
        }

        public static Tuple<Mock<ITextView>, MockRepository> CreateTextViewWithVisibleLines(
            ITextBuffer textBuffer,
            int startLine,
            int? endLine = null,
            int? caretPosition = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var endLineValue = endLine ?? startLine;
            var caretPositionValue = caretPosition ?? textBuffer.GetLine(startLine).Start.Position;
            var caret = factory.Create<ITextCaret>();
            caret.SetupGet(x => x.Position).Returns(
                new CaretPosition(
                    new VirtualSnapshotPoint(textBuffer.GetPoint(caretPositionValue)),
                    factory.Create<IMappingPoint>().Object,
                    PositionAffinity.Predecessor));

            var firstLine = factory.Create<ITextViewLine>();
            firstLine.SetupGet(x => x.Start).Returns(textBuffer.GetLine(startLine).Start);

            var lastLine = factory.Create<ITextViewLine>();
            lastLine.SetupGet(x => x.End).Returns(textBuffer.GetLine(endLineValue).End);

            var lines = factory.Create<ITextViewLineCollection>();
            lines.SetupGet(x => x.FirstVisibleLine).Returns(firstLine.Object);
            lines.SetupGet(x => x.LastVisibleLine).Returns(lastLine.Object);

            var visualBuffer = CreateTextBuffer(factory: factory);
            var textViewModel = factory.Create<ITextViewModel>();
            textViewModel.SetupGet(x => x.VisualBuffer).Returns(visualBuffer.Object);

            var textDataModel = CreateTextDataModel(textBuffer, factory);

            // When creating the CommonOperations linked to the textview, 
            // the roles are checked for the outlining manager.
            // Pretend we don't support anything
            var roles = factory.Create<ITextViewRoleSet>();
            roles.Setup(x => x.Contains(It.IsAny<String>())).Returns(false);

            var properties = new PropertyCollection();
            var textView = factory.Create<ITextView>();
            var options = factory.Create<IEditorOptions>();
            var bufferGraph = factory.Create<IBufferGraph>();
            textView.SetupGet(x => x.TextBuffer).Returns(textBuffer);
            textView.SetupGet(x => x.TextViewLines).Returns(lines.Object);
            textView.SetupGet(x => x.Caret).Returns(caret.Object);
            textView.SetupGet(x => x.InLayout).Returns(false);
            textView.SetupGet(x => x.TextSnapshot).Returns(() => textBuffer.CurrentSnapshot);
            textView.SetupGet(x => x.Properties).Returns(properties);
            textView.SetupGet(x => x.BufferGraph).Returns(bufferGraph.Object);
            textView.SetupGet(x => x.TextViewModel).Returns(textViewModel.Object);
            textView.SetupGet(x => x.VisualSnapshot).Returns(visualBuffer.Object.CurrentSnapshot);
            textView.SetupGet(x => x.Roles).Returns(roles.Object); 
            textView.SetupGet(x => x.Options).Returns(options.Object);
            textView.SetupGet(x => x.TextDataModel).Returns(textDataModel.Object);
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
            mock
                .Setup(x => x.GetService(It.IsAny<Type>()))
                .Returns<Type>(type =>
                    {
                        foreach (var tuple in serviceList)
                        {
                            if (tuple.Item1 == type)
                            {
                                return tuple.Item2;
                            }
                        }
                        return null;
                    });

            return mock;
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
            mock.SetupGet(x => x.LastVisibleLine).Returns(CreateTextViewLine(range.LastLine, factory).Object);
            mock.SetupGet(x => x.Count).Returns(range.Count);
            return mock;
        }
    }
}
