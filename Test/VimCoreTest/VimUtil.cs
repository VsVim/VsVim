using System;
using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using Vim.Interpreter;

namespace Vim.UnitTest
{
    internal static class VimUtil
    {
        internal static RegisterMap CreateRegisterMap(IClipboardDevice device)
        {
            return CreateRegisterMap(device, () => null);
        }

        public static IFoldManager CreateFoldManager(
            ITextView textView,
            IStatusUtil statusUtil = null,
            IOutliningManager outliningManager = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            var option = FSharpOption.CreateForReference(outliningManager);
            return new FoldManager(
                textView,
                new FoldData(textView.TextBuffer),
                statusUtil,
                option);
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
            return new RegisterMap(CreateVimData(), device, func2.ToFSharpFunc());
        }

        internal static IVimData CreateVimData()
        {
            return new VimData(new GlobalSettings());
        }

        internal static Parser CreateParser()
        {
            return new Parser(new GlobalSettings(), CreateVimData());
        }

        internal static CommandBinding CreateNormalBinding(string name)
        {
            return CreateNormalBinding(name, () => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
        }

        internal static CommandBinding CreateNormalBinding(string name, Action del)
        {
            return CreateNormalBinding(
                name,
                unused =>
                {
                    del();
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static CommandBinding CreateNormalBinding(string name, Func<CommandData, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            var command = NormalCommand.NewPing(new PingData(fsharpFunc));
            return CommandBinding.NewNormalBinding(commandName, CommandFlags.None, command);
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
                KeyRemapMode.None,
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
            Func<KeyInput, BindResult<NormalCommand>> func = null;
            func = keyInput =>
            {
                if (predicate(keyInput))
                {
                    var data = new BindData<NormalCommand>(
                        remapMode,
                        func.ToFSharpFunc());
                    return BindResult<NormalCommand>.NewNeedMoreInput(data);
                }

                return BindResult<NormalCommand>.NewComplete(NormalCommand.NewPutAfterCaret(false));
            };

            var bindData = new BindData<NormalCommand>(
                remapMode,
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
            VimKey key = VimKey.None,
            KeyModifiers mod = KeyModifiers.None,
            char? c = null)
        {
            return new KeyInput(
                key,
                mod,
                c.HasValue ? FSharpOption<char>.Some(c.Value) : FSharpOption<char>.None);
        }

        internal static PatternData CreatePatternData(
            string pattern,
            Path path = null)
        {
            path = path ?? Path.Forward;
            return new PatternData(pattern, path);
        }

        internal static SearchData CreateSearchData(
            string pattern,
            SearchKind kind = null,
            SearchOptions options = SearchOptions.None)
        {
            kind = kind ?? SearchKind.Forward;
            return new SearchData(pattern, SearchOffsetData.None, kind, options);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            SnapshotLineRange? range = null,
            SubstituteData data = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            data = data ?? new SubstituteData("a", "b", SubstituteFlags.None);
            return ModeArgument.NewSubstitute(span, range.Value, data);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            string search,
            string replace,
            SubstituteFlags? flags = null,
            SnapshotLineRange? range = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            var data = new SubstituteData(search, replace, flags ?? SubstituteFlags.None);
            return ModeArgument.NewSubstitute(span, range.Value, data);
        }

        internal static MotionResult CreateMotionResult(
            SnapshotSpan span,
            bool isForward = true,
            MotionKind motionKind = null,
            MotionResultFlags flags = MotionResultFlags.None,
            CaretColumn desiredColumn = null)
        {
            motionKind = motionKind ?? MotionKind.CharacterWiseInclusive;
            desiredColumn = desiredColumn ?? CaretColumn.None;
            return new MotionResult(span, span, isForward, motionKind, flags, desiredColumn);
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
            Func<CommandData, CommandResult> func =
                commandData =>
                {
                    action(commandData);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                };
            var data = new PingData(func.ToFSharpFunc());
            return NormalCommand.NewPing(data);
        }

        internal static BindData<T> CreateBindData<T>(Func<KeyInput, BindResult<T>> func = null, KeyRemapMode remapMode = null)
        {
            func = func ?? (x => BindResult<T>.Cancelled);
            return new BindData<T>(remapMode, func.ToFSharpFunc());
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

        internal static VisualSpan CreateVisualSpanCharacter(SnapshotSpan span)
        {
            return VisualSpan.NewCharacter(new CharacterSpan(span));
        }

        internal static LineCommand ParseLineCommand(string text)
        {
            var parser = CreateParser();
            return parser.ParseLineCommand(text);
        }

        internal static ParseResult<Expression> ParseExpression(string expr)
        {
            var parser = CreateParser();
            return parser.ParseExpression(expr);
        }
    }
}
