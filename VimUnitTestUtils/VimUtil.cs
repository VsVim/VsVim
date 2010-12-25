using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using Vim.Modes.Normal;

namespace Vim.UnitTest
{
    internal static class VimUtil
    {
        internal static RegisterMap CreateRegisterMap(IClipboardDevice device)
        {
            return CreateRegisterMap(device, () => null);
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

        internal static Command CreateSimpleCommand(string name)
        {
            return CreateSimpleCommand(name, (count, reg) => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
        }

        internal static Command CreateSimpleCommand(string name, Action<FSharpOption<int>, Register> del)
        {
            return CreateSimpleCommand(
                name,
                (x, y) =>
                {
                    del(x, y);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static Command CreateSimpleCommand(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return Command.NewSimpleCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static Command CreateLongCommand(string name, Func<FSharpOption<int>, Register, LongCommandResult> func, CommandFlags flags = CommandFlags.None)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return Command.NewLongCommand(commandName, flags, fsharpFunc);
        }

        internal static Command CreateLongCommand(string name, Func<KeyInput, bool> func, FSharpOption<KeyRemapMode> keyRemapModeOption = null, CommandFlags flags = CommandFlags.None)
        {
            return CreateLongCommand(
                name,
                (x, y) =>
                {
                    FSharpFunc<KeyInput, LongCommandResult> realFunc = null;
                    Converter<KeyInput, LongCommandResult> func2 = ki =>
                        {
                            if (func(ki))
                            {
                                return LongCommandResult.NewFinished(CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                            }
                            else
                            {
                                return LongCommandResult.NewNeedMoreInput(keyRemapModeOption, realFunc);
                            }
                        };
                    realFunc = func2;
                    return LongCommandResult.NewNeedMoreInput(keyRemapModeOption, realFunc);
                },
                flags);
        }

        internal static Command CreateMotionCommand(string name)
        {
            return CreateMotionCommand(name, (count, reg, motionData) => { });
        }

        internal static Command CreateMotionCommand(string name, Action<FSharpOption<int>, Register, MotionData> del)
        {
            return CreateMotionCommand(
                name,
                (x, y, z) =>
                {
                    del(x, y, z);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static Command CreateMotionCommand(string name, Func<FSharpOption<int>, Register, MotionData, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return Command.NewMotionCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static Command CreateVisualCommand(
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
            return Command.NewVisualCommand(
                KeyNotationUtil.StringToKeyInputSet(name),
                flagsArg,
                kind,
                func.ToFSharpFunc());
        }

        internal static MotionCommand CreateSimpleMotion(
            string name,
            Func<MotionData> func,
            MotionFlags? flags = null)
        {
            var flagsRaw = flags ?? MotionFlags.CursorMovement;
            var commandName = KeyNotationUtil.StringToKeyInputSet(name);
            return MotionCommand.NewSimpleMotionCommand(
                commandName,
                flagsRaw,
                FuncUtil.CreateMotionFunc(func));
        }

        internal static CommandRunData CreateCommandRunData(
            Command command,
            Register register,
            int? count = null,
            MotionRunData motionRunData = null,
            VisualSpan visualRunData = null)
        {
            var countOpt = count != null ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            var motion = motionRunData != null
                ? FSharpOption.Create(motionRunData)
                : FSharpOption<MotionRunData>.None;
            var visual = visualRunData != null
                ? FSharpOption.Create(visualRunData)
                : FSharpOption<VisualSpan>.None;
            return new CommandRunData(
                command,
                register,
                countOpt,
                motion,
                visual);
        }

        internal static MotionRunData CreateMotionRunData(
            MotionCommand motionCommand,
            int? count = null,
            Func<MotionData> func = null)
        {
            func = func ?? (() => null);
            var countOpt = count != null ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            return new MotionRunData(
                motionCommand,
                new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, countOpt),
                FuncUtil.CreateMotionFunc(func));
        }

        internal static VisualSpan CreateVisualSpanSingle(
            SnapshotSpan span,
            VisualKind kind = null)
        {
            return VisualSpan.NewSingle(
                kind ?? VisualKind.Line,
                span);
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
            IOutliningManager outliningManager = null)
        {
            var nav = EditorUtil.FactoryService.textStructureNavigatorSelectorService.GetTextStructureNavigator(textView.TextBuffer);
            search = search ?? new SearchService(EditorUtil.FactoryService.textSearchService, settings.GlobalSettings);
            return new IncrementalSearch(
                textView,
                outliningManager != null ? FSharpOption.Create(outliningManager) : FSharpOption<IOutliningManager>.None,
                settings,
                nav,
                search,
                vimData);
        }

    }
}
