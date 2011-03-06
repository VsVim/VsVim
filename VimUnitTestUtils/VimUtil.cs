using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using Vim.Modes;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    internal static class VimUtil
    {
        internal static ICommonOperations CreateCommonOperations(
            ITextView textView,
            IVimLocalSettings localSettings,
            IOutliningManager outlining = null,
            IStatusUtil statusUtil = null,
            ISearchService searchService = null,
            IUndoRedoOperations undoRedoOperations = null,
            IVimData vimData = null,
            IVimHost vimHost = null,
            ITextStructureNavigator navigator = null,
            IClipboardDevice clipboardDevice = null,
            IFoldManager foldManager = null)
        {
            var editorOperations = EditorUtil.GetOperations(textView);
            var editorOptions = EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(textView);
            var jumpList = new JumpList(new TrackingLineColumnService());
            var keyMap = new KeyMap();
            foldManager = foldManager ?? new FoldManager(textView.TextBuffer);
            statusUtil = statusUtil ?? new StatusUtil();
            searchService = searchService ?? CreateSearchService(localSettings.GlobalSettings);
            undoRedoOperations = undoRedoOperations ??
                                 new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, editorOperations);
            vimData = vimData ?? new VimData();
            vimHost = vimHost ?? new MockVimHost();
            navigator = navigator ?? CreateTextStructureNavigator(textView.TextBuffer);
            clipboardDevice = clipboardDevice ?? new MockClipboardDevice();
            var operationsData = new OperationsData(
                editorOperations,
                editorOptions,
                foldManager,
                jumpList,
                keyMap,
                localSettings,
                outlining != null ? FSharpOption.Create(outlining) : FSharpOption<IOutliningManager>.None,
                CreateRegisterMap(clipboardDevice),
                searchService,
                statusUtil,
                textView,
                undoRedoOperations,
                vimData,
                vimHost,
                navigator);
            return new CommonOperations(operationsData);
        }

        internal static ITextViewMotionUtil CreateTextViewMotionUtil(
            ITextView textView,
            IMarkMap markMap = null,
            IVimLocalSettings settings = null,
            ISearchService search = null,
            ITextStructureNavigator navigator = null,
            IVimData vimData = null)
        {
            markMap = markMap ?? new MarkMap(new TrackingLineColumnService());
            settings = settings ?? new LocalSettings(new GlobalSettings(), textView);
            search = search ?? CreateSearchService(settings.GlobalSettings);
            navigator = navigator ?? CreateTextStructureNavigator(textView.TextBuffer);
            vimData = vimData ?? new VimData();
            return new TextViewMotionUtil(
                textView,
                markMap,
                settings,
                search,
                navigator,
                vimData);
        }

        internal static RegisterMap CreateRegisterMap(IClipboardDevice device)
        {
            return CreateRegisterMap(device, () => null);
        }

        internal static CommandUtil CreateCommandUtil(
            ITextView textView,
            ICommonOperations operations = null,
            ITextViewMotionUtil motionUtil = null,
            IStatusUtil statusUtil = null,
            IRegisterMap registerMap = null,
            IMarkMap markMap = null,
            IVimData vimData = null,
            IVimLocalSettings localSettings = null,
            IUndoRedoOperations undoRedOperations = null,
            ISmartIndentationService smartIndentationService = null,
            IFoldManager foldManager = null,
            IVimHost vimHost = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            undoRedOperations = undoRedOperations ?? VimUtil.CreateUndoRedoOperations(statusUtil);
            localSettings = localSettings ?? new LocalSettings(new GlobalSettings());
            registerMap = registerMap ?? CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            markMap = markMap ?? new MarkMap(new TrackingLineColumnService());
            vimData = vimData ?? new VimData();
            motionUtil = motionUtil ?? CreateTextViewMotionUtil(textView, markMap: markMap, vimData: vimData, settings: localSettings);
            operations = operations ?? CreateCommonOperations(textView, localSettings, vimData: vimData, statusUtil: statusUtil);
            smartIndentationService = smartIndentationService ?? CreateSmartIndentationService();
            foldManager = foldManager ?? CreateFoldManager(textView.TextBuffer);
            vimHost = vimHost ?? new MockVimHost();
            return new CommandUtil(
                textView,
                operations,
                motionUtil,
                statusUtil,
                registerMap,
                markMap,
                vimData,
                localSettings,
                undoRedOperations,
                smartIndentationService,
                foldManager,
                vimHost);
        }

        internal static ISmartIndentationService CreateSmartIndentationService()
        {
            return EditorUtil.FactoryService.SmartIndentationService;
        }

        internal static FoldManager CreateFoldManager(ITextBuffer textBuffer)
        {
            return new FoldManager(textBuffer);
        }

        internal static UndoRedoOperations CreateUndoRedoOperations(IStatusUtil statusUtil = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            return new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, null);
        }

        internal static RegisterMap CreateRegisterMap(IClipboardDevice device, Func<string> func)
        {
            Func<FSharpOption<string>> func2 = () =>
            {
                var result = func();
                return string.IsNullOrEmpty(result) ? FSharpOption<string>.None : FSharpOption.Create(result);
            };
            return new RegisterMap(device, func2.ToFSharpFunc());
        }

        internal static ISearchService CreateSearchService(IVimGlobalSettings settings)
        {
            return new SearchService(EditorUtil.FactoryService.TextSearchService, settings);
        }

        internal static CommandBinding CreateLegacyBinding(string name)
        {
            return CreateLegacyBinding(name, (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
        }

        internal static CommandBinding CreateLegacyBinding(string name, Action<FSharpOption<int>, Register> del)
        {
            return CreateLegacyBinding(
                name,
                (x, y) =>
                {
                    del(x, y);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static CommandBinding CreateLegacyBinding(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return CommandBinding.NewLegacyBinding(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer)
        {
            return EditorUtil.FactoryService.TextStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer);
        }

        internal static CommandRunData CreateCommandRunData(
            Command command = null,
            CommandBinding binding = null,
            CommandResult result = null,
            CommandFlags flags = CommandFlags.None)
        {
            command = command ?? CreateNormalCommand();
            binding = binding ?? CreateNormalBinding(flags: flags);
            result = result ?? CommandResult.NewCompleted(ModeSwitch.NoSwitch);
            return new CommandRunData(binding, command, result);
        }

        internal static CommandBinding CreateNormalBinding(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            NormalCommand command = null)
        {
            command = command ?? NormalCommand.NewPutAfterCaret(false);
            return CommandBinding.NewNormalBinding(KeyNotationUtil.StringToKeyInputSet(name), flags, command);
        }

        internal static CommandBinding CreateMotionBinding(
            string name,
            Func<MotionData, NormalCommand> func)
        {
            return CreateMotionBinding(name, CommandFlags.None, func);
        }

        internal static CommandBinding CreateMotionBinding(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            Func<MotionData, NormalCommand> func = null)
        {
            func = func ?? NormalCommand.NewYank;
            return CommandBinding.NewMotionBinding(KeyNotationUtil.StringToKeyInputSet(name), flags, func.ToFSharpFunc());
        }

        internal static CommandBinding CreateComplexNormalBinding(
            string name,
            Action<KeyInput> action,
            CommandFlags flags = CommandFlags.None)
        {
            Func<KeyInput, BindResult<NormalCommand>> func = keyInput =>
            {
                action(keyInput);
                return BindResult<NormalCommand>.NewComplete(NormalCommand.NewPutAfterCaret(false));
            };

            var bindData = new BindData<NormalCommand>(
                FSharpOption<KeyRemapMode>.None,
                func.ToFSharpFunc());
            var bindDataStorage = BindDataStorage<NormalCommand>.NewSimple(bindData);
            return CommandBinding.NewComplexNormalBinding(
                KeyNotationUtil.StringToKeyInputSet(name),
                flags,
                bindDataStorage);
        }

        internal static CommandBinding CreateComplexNormalBinding(
            string name,
            Func<KeyInput, bool> predicate,
            KeyRemapMode remapMode = null,
            CommandFlags flags = CommandFlags.None)
        {
            var remapModeOption = FSharpOption.CreateForReference(remapMode);
            Func<KeyInput, BindResult<NormalCommand>> func = null;
            func = keyInput =>
            {
                if (predicate(keyInput))
                {
                    var data = new BindData<NormalCommand>(
                        remapModeOption,
                        func.ToFSharpFunc());
                    return BindResult<NormalCommand>.NewNeedMoreInput(data);
                }

                return BindResult<NormalCommand>.NewComplete(NormalCommand.NewPutAfterCaret(false));
            };

            var bindData = new BindData<NormalCommand>(
                remapModeOption,
                func.ToFSharpFunc());
            var bindDataStorage = BindDataStorage<NormalCommand>.NewSimple(bindData);
            return CommandBinding.NewComplexNormalBinding(
                KeyNotationUtil.StringToKeyInputSet(name),
                flags,
                bindDataStorage);
        }


        internal static Command CreateNormalCommand(
            NormalCommand command = null,
            CommandData commandData = null)
        {
            command = command ?? NormalCommand.NewPutAfterCaret(false);
            commandData = commandData ?? new CommandData(FSharpOption<int>.None, FSharpOption<RegisterName>.None);
            return Command.NewNormalCommand(command, commandData);
        }


        internal static MotionData CreateMotionData(
            Motion motion,
            int count)
        {
            return CreateMotionData(motion, new MotionArgument(MotionContext.AfterOperator, FSharpOption.Create(count), FSharpOption<int>.None));
        }

        internal static MotionData CreateMotionData(
            Motion motion,
            MotionArgument argument = null)
        {
            argument = argument ?? new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            return new MotionData(motion, argument);
        }

        internal static KeyInput CreateKeyInput(
            VimKey key = VimKey.NotWellKnown,
            KeyModifiers mod = KeyModifiers.None,
            char? c = null)
        {
            return new KeyInput(
                key,
                mod,
                c.HasValue ? FSharpOption<char>.Some(c.Value) : FSharpOption<char>.None);
        }

        internal static SearchData CreateSearchData(
            string pattern,
            SearchKind kind = SearchKind.Forward,
            SearchOptions options = SearchOptions.None)
        {
            return new SearchData(SearchText.NewPattern(pattern), kind, options);
        }

        internal static SearchData CreateSearchData(
            SearchText text,
            SearchKind kind = SearchKind.Forward,
            SearchOptions options = SearchOptions.None)
        {
            return new SearchData(text, kind, options);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            SnapshotLineRange range = null,
            SubstituteData data = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            data = data ?? new SubstituteData("a", "b", SubstituteFlags.None);
            return ModeArgument.NewSubsitute(span, range, data);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            string search,
            string replace,
            SubstituteFlags? flags = null,
            SnapshotLineRange range = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            var data = new SubstituteData(search, replace, flags ?? SubstituteFlags.None);
            return ModeArgument.NewSubsitute(span, range, data);
        }

        internal static IIncrementalSearch CreateIncrementalSearch(
            ITextView textView,
            IVimLocalSettings settings,
            IVimData vimData,
            ISearchService search = null,
            IOutliningManager outliningManager = null,
            IStatusUtil statusUtil = null)
        {
            vimData = vimData ?? new VimData();
            search = search ?? CreateSearchService(settings.GlobalSettings);
            statusUtil = statusUtil ?? new StatusUtil();
            var nav = CreateTextStructureNavigator(textView.TextBuffer);
            var operations = CreateCommonOperations(
                textView: textView,
                localSettings: settings,
                outlining: outliningManager,
                vimData: vimData);
            return new IncrementalSearch(
                operations,
                settings,
                nav,
                search,
                statusUtil,
                vimData);
        }

        internal static MotionResult CreateMotionResult(
            SnapshotSpan span,
            bool isForward,
            MotionKind motionKind,
            OperationKind operationKind,
            int? column = null)
        {
            return CreateMotionResult(
                span: span,
                isForward: isForward,
                isAnyWord: false,
                motionKind: motionKind,
                operationKind: operationKind,
                column: column);
        }

        internal static MotionResult CreateMotionResult(
            SnapshotSpan span,
            bool isForward = true,
            bool isAnyWord = false,
            MotionKind motionKind = null,
            OperationKind operationKind = null,
            int? column = null)
        {
            motionKind = motionKind ?? MotionKind.Inclusive;
            operationKind = operationKind ?? OperationKind.CharacterWise;
            var col = column.HasValue ? FSharpOption.Create(column.Value) : FSharpOption<int>.None;
            return new MotionResult(span, isForward, isAnyWord, motionKind, operationKind, col);
        }

        internal static CommandData CreateCommandData(
            int? count = null,
            RegisterName name = null)
        {
            return new CommandData(
                FSharpOption.CreateForNullable(count),
                FSharpOption.CreateForReference(name));
        }

        internal static NormalCommand CreatePing(Action<CommandData> action)
        {
            var data = new PingData(action.ToFSharpFunc());
            return NormalCommand.NewPing(data);
        }

        internal static BindData<T> CreateBindData<T>(Func<KeyInput, BindResult<T>> func = null, KeyRemapMode remapMode = null)
        {
            func = func ?? (x => BindResult<T>.Cancelled);
            return new BindData<T>(FSharpOption.CreateForReference(remapMode), func.ToFSharpFunc());
        }

        internal static BindDataStorage<T> CreateBindDataStorage<T>(BindData<T> bindData = null)
        {
            bindData = bindData ?? CreateBindData<T>();
            return BindDataStorage<T>.NewSimple(bindData);
        }

        internal static StringData CreateStringDataBlock(params string[] values)
        {
            return StringData.NewBlock(NonEmptyCollectionUtil.OfSeq(values).Value);
        }
    }
}
