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
                EditorUtil.FactoryService.SmartIndentationService,
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
            IVimData vimData = null)
        {
            var localSettings = new LocalSettings(new GlobalSettings());
            registerMap = registerMap ?? CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            markMap = markMap ?? new MarkMap(new TrackingLineColumnService());
            vimData = vimData ?? new VimData();
            motionUtil = motionUtil ?? CreateTextViewMotionUtil(textView, markMap: markMap, vimData: vimData, settings: localSettings);
            operations = operations ?? CreateCommonOperations(textView, localSettings, vimData: vimData, statusUtil: statusUtil);
            return new CommandUtil(
                textView,
                operations,
                motionUtil,
                statusUtil,
                registerMap,
                markMap,
                vimData,
                localSettings);
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

        internal static CommandBinding CreateSimpleCommand(string name)
        {
            return CreateSimpleCommand(name, (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
        }

        internal static CommandBinding CreateSimpleCommand(string name, Action<FSharpOption<int>, Register> del)
        {
            return CreateSimpleCommand(
                name,
                (x, y) =>
                {
                    del(x, y);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static CommandBinding CreateSimpleCommand(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return CommandBinding.NewLegacySimpleCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer)
        {
            return EditorUtil.FactoryService.TextStructureNavigatorSelectorService.GetTextStructureNavigator(textBuffer);
        }

        internal static CommandBinding CreateMotionCommand(string name)
        {
            return CreateMotionCommand(name, (count, reg, motionData) => { });
        }

        internal static CommandBinding CreateMotionCommand(string name, Action<FSharpOption<int>, Register, MotionResult> del)
        {
            return CreateMotionCommand(
                name,
                (x, y, z) =>
                {
                    del(x, y, z);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static CommandBinding CreateMotionCommand(string name, Func<FSharpOption<int>, Register, MotionResult, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return CommandBinding.NewLegacyMotionCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static CommandBinding CreateVisualCommand(
            string name = "c",
            CommandFlags? flags = null,
            VisualKind kind = null,
            Func<FSharpOption<int>, Register, VisualSpan, CommandResult> func = null)
        {
            var flagsArg = flags ?? CommandFlags.None;
            kind = kind ?? VisualKind.Line;
            if (func == null)
            {
                func = (x, y, z) => CommandResult.NewCompleted(ModeSwitch.NoSwitch);
            }
            return CommandBinding.NewLegacyVisualCommand(
                KeyNotationUtil.StringToKeyInputSet(name),
                flagsArg,
                kind,
                func.ToFSharpFunc());
        }

        internal static MotionBinding CreateSimpleMotion(
            string name,
            Motion motion,
            MotionFlags? flags = null)
        {
            var flagsRaw = flags ?? MotionFlags.CursorMovement;
            var commandName = KeyNotationUtil.StringToKeyInputSet(name);
            return MotionBinding.NewSimple(
                commandName,
                flagsRaw,
                motion);
        }

        internal static CommandRunData CreateCommandRunData(
            Command command = null,
            CommandBinding binding = null,
            CommandResult result = null,
            CommandFlags flags = CommandFlags.None)
        {
            command = command ?? CreateNormalCommand();
            binding = binding ?? CreateCommandBindingNormal(flags: flags);
            result = result ?? CommandResult.NewCompleted(ModeSwitch.NoSwitch);
            return new CommandRunData(binding, command, result);
        }

        internal static CommandBinding CreateCommandBindingNormal(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            NormalCommand command = null)
        {
            command = command ?? NormalCommand.NewPutAfterCaret(false);
            return CommandBinding.NewNormalCommand(KeyNotationUtil.StringToKeyInputSet(name), flags, command);
        }

        internal static CommandBinding CreateCommandBindingMotion(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            Func<MotionData, NormalCommand> func = null)
        {
            func = func ?? NormalCommand.NewYank;
            return CommandBinding.NewMotionCommand(KeyNotationUtil.StringToKeyInputSet(name), flags, func.ToFSharpFunc());
        }

        internal static CommandBinding CreateCommandBindingNormalComplex(
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
            return CommandBinding.NewComplexNormalCommand(
                KeyNotationUtil.StringToKeyInputSet(name),
                flags,
                bindDataStorage);
        }

        internal static CommandBinding CreateCommandBindingNormalComplex(
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
            return CommandBinding.NewComplexNormalCommand(
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
    }
}
