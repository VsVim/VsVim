using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;
using Expression = Vim.Interpreter.Expression;
using Size = System.Windows.Size;

namespace Vim.UnitTest
{
    /// <summary>
    /// Contains extension methods common to all unit test
    /// </summary>
    public static class Extensions
    {
        #region CommandResult

        public static CommandResult.Completed AsCompleted(this CommandResult result)
        {
            Assert.True(result.IsCompleted);
            return (CommandResult.Completed)result;
        }

        #endregion

        #region CommandData

        public static RegisterName GetRegisterNameOrDefault(this CommandData commandData)
        {
            return commandData.RegisterName.IsSome()
                ? commandData.RegisterName.Value
                : RegisterName.Unnamed;
        }

        public static Register GetRegister(this CommandData commandData, IRegisterMap registerMap)
        {
            return registerMap.GetRegister(commandData.GetRegisterNameOrDefault());
        }

        #endregion

        #region MaintainCaretColumn

        internal static MaintainCaretColumn.Spaces AsSpaces(this MaintainCaretColumn result)
        {
            Assert.True(result.IsSpaces);
            return (MaintainCaretColumn.Spaces)result;
        }

        #endregion

        #region VisualSelection

        public static void SelectAndMoveCaret(this VisualSelection selection, ITextView textView)
        {
            selection.Select(textView);
            TextViewUtil.MoveCaretToPointRaw(textView, selection.GetCaretPoint(SelectionKind.Inclusive), MoveCaretFlags.EnsureOnScreen);
        }

        #endregion

        #region LineCommand

        /// <summary>
        /// LineCommand as ChangeDirectory
        /// </summary>
        public static LineCommand.ChangeDirectory AsChangeDirectory(this LineCommand lineCommand)
        {
            return (LineCommand.ChangeDirectory)lineCommand;
        }

        /// <summary>
        /// LineCommand as ChangeLocalDirectory
        /// </summary>
        public static LineCommand.ChangeLocalDirectory AsChangeLocalDirectory(this LineCommand lineCommand)
        {
            return (LineCommand.ChangeLocalDirectory)lineCommand;
        }

        /// <summary>
        /// LineCommand as Close
        /// </summary>
        public static LineCommand.Close AsClose(this LineCommand lineCommand)
        {
            return (LineCommand.Close)lineCommand;
        }

        /// <summary>
        /// LineCommand as Delete
        /// </summary>
        public static LineCommand.Delete AsDelete(this LineCommand lineCommand)
        {
            return (LineCommand.Delete)lineCommand;
        }

        /// <summary>
        /// LineCommand as ReadCommand
        /// </summary>
        public static LineCommand.ReadCommand AsReadCommand(this LineCommand lineCommand)
        {
            return (LineCommand.ReadCommand)lineCommand;
        }

        /// <summary>
        /// LineCommand as ReadFile
        /// </summary>
        public static LineCommand.ReadFile AsReadFile(this LineCommand lineCommand)
        {
            return (LineCommand.ReadFile)lineCommand;
        }

        /// <summary>
        /// LineCommand as Set
        /// </summary>
        public static LineCommand.Set AsSet(this LineCommand lineCommand)
        {
            return (LineCommand.Set)lineCommand;
        }

        /// <summary>
        /// LineCommand as Source
        /// </summary>
        public static LineCommand.Source AsSource(this LineCommand lineCommand)
        {
            return (LineCommand.Source)lineCommand;
        }

        /// <summary>
        /// LineCommand as Substitute
        /// </summary>
        public static LineCommand.Substitute AsSubstitute(this LineCommand lineCommand)
        {
            return (LineCommand.Substitute)lineCommand;
        }

        /// <summary>
        /// LineCommand as SubstituteRepeat
        /// </summary>
        public static LineCommand.SubstituteRepeat AsSubstituteRepeat(this LineCommand lineCommand)
        {
            return (LineCommand.SubstituteRepeat)lineCommand;
        }

        /// <summary>
        /// LineCommand as MapKeys
        /// </summary>
        public static LineCommand.MapKeys AsMapKeys(this LineCommand lineCommand)
        {
            return (LineCommand.MapKeys)lineCommand;
        }

        /// <summary>
        /// LineCommand as UnmapKeys
        /// </summary>
        public static LineCommand.UnmapKeys AsUnmapKeys(this LineCommand lineCommand)
        {
            return (LineCommand.UnmapKeys)lineCommand;
        }

        /// <summary>
        /// LineCommand as Write
        /// </summary>
        public static LineCommand.Write AsWrite(this LineCommand lineCommand)
        {
            return (LineCommand.Write)lineCommand;
        }

        /// <summary>
        /// LineCommand as WriteAll
        /// </summary>
        public static LineCommand.WriteAll AsWriteAll(this LineCommand lineCommand)
        {
            return (LineCommand.WriteAll)lineCommand;
        }

        /// <summary>
        /// LineCommand as QuickFixNext
        /// </summary>
        public static LineCommand.QuickFixNext AsQuickFixNext(this LineCommand lineCommand)
        {
            return (LineCommand.QuickFixNext)lineCommand;
        }

        /// <summary>
        /// LineCommand as QuickFixPrevious
        /// </summary>
        public static LineCommand.QuickFixPrevious AsQuickFixPrevious(this LineCommand lineCommand)
        {
            return (LineCommand.QuickFixPrevious)lineCommand;
        }

        #endregion

        #region SetArgument

        /// <summary>
        /// SetArgument as SetArgument
        /// </summary>
        public static SetArgument.DisplaySetting AsDisplaySetting(this SetArgument setArgument)
        {
            return (SetArgument.DisplaySetting)setArgument;
        }

        /// <summary>
        /// SetArgument as UseSetting
        /// </summary>
        public static SetArgument.UseSetting AsUseSetting(this SetArgument setArgument)
        {
            return (SetArgument.UseSetting)setArgument;
        }

        /// <summary>
        /// SetArgument as ToggleOffSetting
        /// </summary>
        public static SetArgument.ToggleOffSetting AsToggleOffSetting(this SetArgument setArgument)
        {
            return (SetArgument.ToggleOffSetting)setArgument;
        }

        /// <summary>
        /// SetArgument as InvertSetting
        /// </summary>
        public static SetArgument.InvertSetting AsInvertSetting(this SetArgument setArgument)
        {
            return (SetArgument.InvertSetting)setArgument;
        }

        /// <summary>
        /// SetArgument as AssignSetting
        /// </summary>
        public static SetArgument.AssignSetting AsAssignSetting(this SetArgument setArgument)
        {
            return (SetArgument.AssignSetting)setArgument;
        }

        /// <summary>
        /// SetArgument as AddSetting
        /// </summary>
        public static SetArgument.AddSetting AsAddSetting(this SetArgument setArgument)
        {
            return (SetArgument.AddSetting)setArgument;
        }

        /// <summary>
        /// SetArgument as MultiplySetting
        /// </summary>
        public static SetArgument.MultiplySetting AsMultiplySetting(this SetArgument setArgument)
        {
            return (SetArgument.MultiplySetting)setArgument;
        }

        /// <summary>
        /// SetArgument as SubtractSetting
        /// </summary>
        public static SetArgument.SubtractSetting AsSubtractSetting(this SetArgument setArgument)
        {
            return (SetArgument.SubtractSetting)setArgument;
        }

        #endregion

        #region LineRange

        /// <summary>
        /// LineRange as SingleLine
        /// </summary>
        public static LineRangeSpecifier.SingleLine AsSingleLine(this LineRangeSpecifier lineRange)
        {
            return (LineRangeSpecifier.SingleLine)lineRange;
        }

        /// <summary>
        /// LineRange as Range
        /// </summary>
        public static LineRangeSpecifier.Range AsRange(this LineRangeSpecifier lineRange)
        {
            return (LineRangeSpecifier.Range)lineRange;
        }

        /// <summary>
        /// LineRange as WithEndCount
        /// </summary>
        public static LineRangeSpecifier.WithEndCount AsWithEndCount(this LineRangeSpecifier lineRange)
        {
            return (LineRangeSpecifier.WithEndCount)lineRange;
        }

        #endregion

        #region LineSpecifier

        /// <summary>
        /// LineSpecifier as Number
        /// </summary>
        public static LineSpecifier.Number AsNumber(this LineSpecifier lineSpecifier)
        {
            return (LineSpecifier.Number)lineSpecifier;
        }

        /// <summary>
        /// Is thise a Number with the specified value
        /// </summary>
        public static bool IsNumber(this LineSpecifier lineSpecifier, int number)
        {
            return lineSpecifier.IsNumber && lineSpecifier.AsNumber().Item == number;
        }

        /// <summary>
        /// LineSpecifier as NextLineWithPattern
        /// </summary>
        public static LineSpecifier.NextLineWithPattern AsNextLineWithPattern(this LineSpecifier lineSpecifier)
        {
            return (LineSpecifier.NextLineWithPattern)lineSpecifier;
        }

        /// <summary>
        /// LineSpecifier as PreviousLineWithPattern
        /// </summary>
        public static LineSpecifier.PreviousLineWithPattern AsPreviousLineWithPattern(this LineSpecifier lineSpecifier)
        {
            return (LineSpecifier.PreviousLineWithPattern)lineSpecifier;
        }

        #endregion

        #region ParseResult

        /// <summary>
        /// Get the suceeded version of the ParseResult
        /// </summary>
        public static ParseResult<T>.Succeeded AsSucceeded<T>(this ParseResult<T> parseResult)
        {
            return (ParseResult<T>.Succeeded)parseResult;
        }

        /// <summary>
        /// Get the failed version of the ParseResult
        /// </summary>
        public static ParseResult<T>.Failed AsFailed<T>(this ParseResult<T> parseResult)
        {
            return (ParseResult<T>.Failed)parseResult;
        }

        /// <summary>
        /// Is this a failed ParseResult with the given error message?
        /// </summary>
        public static bool IsFailed<T>(this ParseResult<T> parseResult, string message)
        {
            return parseResult.IsFailed && message == parseResult.AsFailed().Item;
        }

        #endregion

        #region Expression

        /// <summary>
        /// Get the suceeded version of the Expression
        /// </summary>
        public static Expression.ConstantValue AsConstantValue(this Expression expr)
        {
            return (Expression.ConstantValue)expr;
        }

        #endregion

        #region VariableValue

        /// <summary>
        /// Number version of a value
        /// </summary>
        public static VariableValue.Number AsNumber(this VariableValue value)
        {
            return (VariableValue.Number)value;
        }

        /// <summary>
        /// String version of a value
        /// </summary>
        public static VariableValue.String AsString(this VariableValue value)
        {
            return (VariableValue.String)value;
        }

        #endregion

        #region ModeSwitch

        public static ModeSwitch.SwitchModeWithArgument AsSwitchModeWithArgument(this ModeSwitch mode)
        {
            Assert.True(mode.IsSwitchModeWithArgument);
            return (ModeSwitch.SwitchModeWithArgument)mode;
        }

        #endregion

        #region ProcessResult

        public static ProcessResult.Handled AsHandled(this ProcessResult res)
        {
            return (ProcessResult.Handled)res;
        }

        public static bool IsSwitchModeOneTimeCommand(this ProcessResult result)
        {
            return result.IsHandled && result.AsHandled().Item.IsSwitchModeOneTimeCommand;
        }

        public static bool IsSwitchMode(this ProcessResult result, ModeKind kind)
        {
            return result.IsHandled && result.AsHandled().Item.IsSwitchMode(kind);
        }

        public static bool IsSwitchModeWithArgument(this ProcessResult result, ModeKind kind, ModeArgument argument)
        {
            return result.IsHandled && result.AsHandled().Item.IsSwitchModeWithArgument(kind, argument);
        }

        public static bool IsSwitchPreviousMode(this ProcessResult result)
        {
            return result.IsHandled && result.AsHandled().Item.IsSwitchPreviousMode;
        }

        public static bool IsHandledNoSwitch(this ProcessResult result)
        {
            return result.IsHandled && result.AsHandled().Item.IsNoSwitch;
        }

        #endregion

        #region NumberValue

        /// <summary>
        /// Convert the NumberValue to the Decimal form
        /// </summary>
        internal static NumberValue.Decimal AsDecimal(this NumberValue numberValue)
        {
            return (NumberValue.Decimal)numberValue;
        }

        /// <summary>
        /// Convert the NumberValue to the Hex form
        /// </summary>
        internal static NumberValue.Hex AsHex(this NumberValue numberValue)
        {
            return (NumberValue.Hex)numberValue;
        }

        /// <summary>
        /// Convert the NumberValue to the Octal form
        /// </summary>
        internal static NumberValue.Octal AsOctal(this NumberValue numberValue)
        {
            return (NumberValue.Octal)numberValue;
        }

        /// <summary>
        /// Convert the NumberValue to the Alpha form
        /// </summary>
        internal static NumberValue.Alpha AsAlpha(this NumberValue numberValue)
        {
            return (NumberValue.Alpha)numberValue;
        }

        #endregion

        #region ModeSwitch

        public static bool IsSwitchMode(this ModeSwitch mode, ModeKind kind)
        {
            return mode.IsSwitchMode && ((ModeSwitch.SwitchMode)mode).Item == kind;
        }

        public static bool IsSwitchModeWithArgument(this ModeSwitch mode, ModeKind kind, ModeArgument argument)
        {
            if (!mode.IsSwitchModeWithArgument)
            {
                return false;
            }

            var value = (ModeSwitch.SwitchModeWithArgument)mode;
            return value.Item1 == kind && value.Item2.Equals(argument);
        }

        #endregion

        #region BindResult

        public static BindResult<T>.Complete AsComplete<T>(this BindResult<T> res)
        {
            Assert.True(res.IsComplete);
            return (BindResult<T>.Complete)res;
        }

        public static BindResult<T>.NeedMoreInput AsNeedMoreInput<T>(this BindResult<T> res)
        {
            Assert.True(res.IsNeedMoreInput);
            return (BindResult<T>.NeedMoreInput)res;
        }

        public static BindResult<TResult> Convert<T, TResult>(this BindResult<T> res, Func<T, TResult> func)
        {
            var func2 = func.ToFSharpFunc();
            return res.Convert(func2);
        }

        #endregion

        #region UndoRedoData

        internal static UndoRedoData.Normal AsNormal(this UndoRedoData data)
        {
            return (UndoRedoData.Normal)data;
        }

        internal static UndoRedoData.Linked AsLinked(this UndoRedoData data)
        {
            return (UndoRedoData.Linked)data;
        }

        #endregion

        #region Command

        public static Command.VisualCommand AsVisualCommand(this Command command)
        {
            Assert.True(command.IsVisualCommand);
            return (Command.VisualCommand)command;
        }

        public static Command.NormalCommand AsNormalCommand(this Command command)
        {
            Assert.True(command.IsNormalCommand);
            return (Command.NormalCommand)command;
        }

        public static Command.InsertCommand AsInsertCommand(this Command command)
        {
            Assert.True(command.IsInsertCommand);
            return (Command.InsertCommand)command;
        }

        #endregion

        #region InsertCommand

        public static InsertCommand.DirectInsert AsDirectInsert(this InsertCommand command)
        {
            return (InsertCommand.DirectInsert)command;
        }

        #endregion

        #region IMotionCapture

        public static BindResult<Tuple<Motion, FSharpOption<int>>> GetMotionAndCount(this IMotionCapture capture, char c)
        {
            return capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput(c));
        }

        #endregion

        #region ModeUtil.Result

        public static Result.Failed AsFailed(this Result res)
        {
            return (Result.Failed)res;
        }

        #endregion

        #region IKeyMap

        public static IEnumerable<KeyInput> GetKeyMapping(this IKeyMap keyMap, char c, KeyRemapMode mode)
        {
            return GetKeyMapping(keyMap, KeyInputUtil.CharToKeyInput(c), mode);
        }

        public static IEnumerable<KeyInput> GetKeyMapping(this IKeyMap keyMap, string str, KeyRemapMode mode)
        {
            return GetKeyMapping(keyMap, KeyNotationUtil.StringToKeyInputSet(str), mode);
        }

        public static IEnumerable<KeyInput> GetKeyMapping(this IKeyMap keyMap, KeyInput ki, KeyRemapMode mode)
        {
            return GetKeyMapping(keyMap, KeyInputSet.NewOneKeyInput(ki), mode);
        }

        public static IEnumerable<KeyInput> GetKeyMapping(this IKeyMap keyMap, KeyInputSet kiSet, KeyRemapMode mode)
        {
            return keyMap.GetKeyMapping(kiSet, mode).AsMapped().Item.KeyInputs;
        }

        public static KeyMappingResult GetKeyMappingResult(this IKeyMap keyMap, KeyInput ki, KeyRemapMode mode)
        {
            return GetKeyMappingResult(keyMap, KeyInputSet.NewOneKeyInput(ki), mode);
        }

        public static KeyMappingResult GetKeyMappingResult(this IKeyMap keyMap, KeyInputSet set, KeyRemapMode mode)
        {
            return keyMap.GetKeyMapping(set, mode);
        }

        public static KeyMappingResult GetKeyMappingResult(this IKeyMap keyMap, char c, KeyRemapMode mode)
        {
            return GetKeyMappingResult(keyMap, KeyInputUtil.CharToKeyInput(c), mode);
        }

        public static KeyMappingResult GetKeyMappingResult(this IKeyMap keyMap, string str, KeyRemapMode mode)
        {
            return keyMap.GetKeyMapping(KeyNotationUtil.StringToKeyInputSet(str), mode);
        }

        #endregion

        #region KeyMappingResult

        public static KeyMappingResult.Mapped AsMapped(this KeyMappingResult res)
        {
            Assert.True(res.IsMapped);
            return (KeyMappingResult.Mapped)res;
        }

        public static KeyMappingResult.PartiallyMapped AsPartiallyMapped(this KeyMappingResult res)
        {
            Assert.True(res.IsPartiallyMapped);
            return (KeyMappingResult.PartiallyMapped)res;
        }

        public static KeyInputSet GetMappedKeyInputs(this KeyMappingResult res)
        {
            if (res.IsMapped)
            {
                return res.AsMapped().Item;
            }

            var partialMap = res.AsPartiallyMapped();
            return KeyInputSetUtil.Combine(partialMap.item1, partialMap.item2);
        }

        #endregion

        #region SettingValue

        public static SettingValue.String AsString(this SettingValue value)
        {
            Assert.True(value.IsString);
            return (SettingValue.String)value;
        }

        public static SettingValue.Toggle AsToggle(this SettingValue value)
        {
            Assert.True(value.IsToggle);
            return (SettingValue.Toggle)value;
        }

        public static SettingValue.Number AsNumber(this SettingValue value)
        {
            Assert.True(value.IsNumber);
            return (SettingValue.Number)value;
        }

        #endregion

        #region RunResult

        public static RunResult.SubstituteConfirm AsSubstituteConfirm(this RunResult result)
        {
            Assert.True(result.IsSubstituteConfirm);
            return (RunResult.SubstituteConfirm)result;
        }

        #endregion

        #region IMode

        public static bool CanProcess(this IMode mode, VimKey key)
        {
            return mode.CanProcess(KeyInputUtil.VimKeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, VimKey key)
        {
            return mode.Process(KeyInputUtil.VimKeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, char c)
        {
            return mode.Process((KeyInputUtil.CharToKeyInput(c)));
        }

        public static ProcessResult Process(this IMode mode, string input, bool enter = false)
        {
            ProcessResult last = null;
            foreach (var c in input)
            {
                var i = KeyInputUtil.CharToKeyInput(c);
                last = mode.Process(c);
            }

            if (enter)
            {
                last = mode.Process(KeyInputUtil.EnterKey);
            }

            return last;
        }

        /// <summary>
        /// Process the full notation as a series of KeyInput values
        /// </summary>
        public static void ProcessNotation(this IMode mode, string notation, bool enter = false)
        {
            var keyInputSet = KeyNotationUtil.StringToKeyInputSet(notation);
            foreach (var keyInput in keyInputSet.KeyInputs)
            {
                mode.Process(keyInput);
            }

            if (enter)
            {
                mode.Process(KeyInputUtil.EnterKey);
            }
        }

        #endregion

        #region IVimBufferFactory

        internal static IVimBuffer CreateVimBuffer(this IVimBufferFactory vimBufferFactory, ITextView textView, IVimTextBuffer vimTextBuffer)
        {
            var vimBufferData = vimBufferFactory.CreateVimBufferData(vimTextBuffer, textView);
            return vimBufferFactory.CreateVimBuffer(vimBufferData);
        }

        #endregion

        #region IVimBuffer

        /// <summary>
        /// Helper for the CanProcess function which maps the char to a KeyInput value
        /// </summary>
        public static bool CanProcess(this IVimBuffer buffer, char c)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            return buffer.CanProcess(keyInput);
        }

        /// <summary>
        /// Helper for the CanProcess function which maps the VimKey to a KeyInput value
        /// </summary>
        public static bool CanProcess(this IVimBuffer buffer, VimKey key)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(key);
            return buffer.CanProcess(keyInput);
        }

        /// <summary>
        /// Helper for the CanProcessAsCommand function which maps the char to a KeyInput value
        /// </summary>
        public static bool CanProcessAsCommand(this IVimBuffer buffer, char c)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            return buffer.CanProcessAsCommand(keyInput);
        }

        /// <summary>
        /// Helper for the CanProcessAsCommand function which maps the VimKey to a KeyInput value
        /// </summary>
        public static bool CanProcessAsCommand(this IVimBuffer buffer, VimKey key)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(key);
            return buffer.CanProcessAsCommand(keyInput);
        }

        /// <summary>
        /// Process the VimKey values in sequence
        /// </summary>
        public static bool Process(this IVimBuffer buf, params VimKey[] keys)
        {
            var ret = false;
            foreach (var key in keys)
            {
                ret = buf.Process(KeyInputUtil.VimKeyToKeyInput(key)).IsAnyHandled;
            }
            return ret;
        }

        public static bool Process(this IVimBuffer buf, char c)
        {
            return buf.Process(KeyInputUtil.CharToKeyInput(c)).IsAnyHandled;
        }

        public static void Process(this IVimBuffer buf, string input, bool enter = false)
        {
            foreach (var c in input)
            {
                var i = KeyInputUtil.CharToKeyInput(c);
                buf.Process(i);
            }

            if (enter)
            {
                buf.Process(KeyInputUtil.EnterKey);
            }
        }

        /// <summary>
        /// Process the full notation as a series of KeyInput values
        /// </summary>
        public static void ProcessNotation(this IVimBuffer vimBuffer, string notation, bool enter = false)
        {
            var keyInputSet = KeyNotationUtil.StringToKeyInputSet(notation);
            foreach (var keyInput in keyInputSet.KeyInputs)
            {
                vimBuffer.Process(keyInput);
            }

            if (enter)
            {
                vimBuffer.Process(KeyInputUtil.EnterKey);
            }
        }

        public static Register GetRegister(this IVimBuffer buffer, char c)
        {
            var name = RegisterNameUtil.CharToRegister(c).Value;
            return buffer.RegisterMap.GetRegister(name);
        }

        #endregion

        #region ITextSnapshot

        public static ITextSnapshotLine GetLine(this ITextSnapshot snapshot, int lineNumber)
        {
            return snapshot.GetLineFromLineNumber(lineNumber);
        }

        public static ITextSnapshotLine GetFirstLine(this ITextSnapshot snapshot)
        {
            return GetLine(snapshot, 0);
        }

        public static ITextSnapshotLine GetLastLine(this ITextSnapshot snapshot)
        {
            return GetLine(snapshot, snapshot.LineCount - 1);
        }

        #endregion

        #region ITextBuffer

        public static SnapshotPoint GetStartPoint(this ITextBuffer textBuffer)
        {
            return textBuffer.CurrentSnapshot.GetStartPoint();
        }

        public static SnapshotPoint GetEndPoint(this ITextBuffer textBuffer)
        {
            return textBuffer.CurrentSnapshot.GetEndPoint();
        }

        public static ITextSnapshotLine GetLineFromLineNumber(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        public static ITextSnapshotLine GetLine(this ITextBuffer textBuffer, int lineNumber)
        {
            return textBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);
        }

        public static BlockSpan GetBlockSpan(this ITextBuffer textBuffer, int column, int length, int startLine = 0, int lineCount = 1)
        {
            var line = textBuffer.GetLine(startLine);
            var startPoint = line.Start.Add(column);
            return new BlockSpan(startPoint, length, lineCount);
        }

        public static NonEmptyCollection<SnapshotSpan> GetBlock(this ITextBuffer textBuffer, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return GetBlockSpan(textBuffer, column, length, startLine, lineCount).BlockSpans;
        }

        public static VisualSpan GetVisualSpanBlock(this ITextBuffer textBuffer, int column, int length, int startLine = 0, int lineCount = 1)
        {
            var blockSpanData = GetBlockSpan(textBuffer, column, length, startLine, lineCount);
            return VisualSpan.NewBlock(blockSpanData);
        }

        public static IEnumerable<string> GetLines(this ITextBuffer textBuffer)
        {
            foreach (var line in textBuffer.CurrentSnapshot.Lines)
            {
                yield return line.GetText();
            }
        }

        public static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int length)
        {
            return GetLineSpan(buffer, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int column, int length)
        {
            var line = buffer.GetLine(lineNumber);
            return new SnapshotSpan(line.Start.Add(column), length);
        }

        #endregion

        #region ITextView

        public static SnapshotPoint GetStartPoint(this ITextView textView)
        {
            return textView.TextBuffer.CurrentSnapshot.GetStartPoint();
        }

        public static SnapshotPoint GetEndPoint(this ITextView textView)
        {
            return textView.TextBuffer.CurrentSnapshot.GetEndPoint();
        }

        public static SnapshotPoint GetPoint(this ITextView textView, int position)
        {
            return textView.TextBuffer.GetPoint(position);
        }

        public static SnapshotPoint GetPointInLine(this ITextView textView, int lineNumber, int column)
        {
            return textView.TextBuffer.GetPointInLine(lineNumber, column);
        }

        public static SnapshotLineRange GetLineRange(this ITextView textView, int startLine, int endLine = -1)
        {
            return textView.TextBuffer.GetLineRange(startLine, endLine);
        }

        public static ITextSnapshotLine GetLine(this ITextView textView, int lineNumber)
        {
            return textView.TextBuffer.GetLine(lineNumber);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int length)
        {
            return GetLineSpan(textView, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int column, int length)
        {
            return GetLineSpan(textView.TextBuffer, lineNumber, column, length);
        }

        /// <summary>
        /// Change the selection to be the specified SnapshotSpan value and update the caret to be on the
        /// last included point in the SnapshotSpan.  
        /// </summary>
        public static void SelectAndMoveCaret(this ITextView textView, SnapshotSpan span)
        {
            var characterSpan = CharacterSpan.CreateForSpan(span);
            var visualSelection = VisualSelection.CreateForward(VisualSpan.NewCharacter(characterSpan));
            visualSelection.SelectAndMoveCaret(textView);
        }

        public static VisualSpan GetVisualSpanBlock(this ITextView textView, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return GetVisualSpanBlock(textView.TextBuffer, column, length, startLine, lineCount);
        }

        public static BlockSpan GetBlockSpan(this ITextView textView, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return textView.TextBuffer.GetBlockSpan(column, length, startLine, lineCount);
        }

        public static NonEmptyCollection<SnapshotSpan> GetBlock(this ITextView textView, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return GetBlock(textView.TextBuffer, column, length, startLine, lineCount);
        }

        public static ITextSnapshotLine GetLastLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetLastLine();
        }

        public static ITextSnapshotLine GetFirstLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetFirstLine();
        }

        public static void SetText(this ITextView textView, string text, int? caret)
        {
            textView.TextBuffer.SetText(text);
            if (caret.HasValue)
            {
                textView.MoveCaretTo(caret.Value);
            }
        }

        #endregion

        #region IWpfTextView

        /// <summary>
        /// Make only a single line visible in the IWpfTextView.  This is really useful when testing
        /// actions like scrolling
        /// </summary>
        /// <param name="wpfTextView"></param>
        public static void MakeOneLineVisible(this IWpfTextView wpfTextView)
        {
            var oldSize = wpfTextView.VisualElement.RenderSize;
            var size = new Size(
                oldSize.Width,
                wpfTextView.TextViewLines.FirstVisibleLine.Height);
            wpfTextView.VisualElement.RenderSize = size;
        }

        #endregion

        #region VisualSpan

        public static VisualSpan.Character AsCharacter(this VisualSpan span)
        {
            Assert.True(span.IsCharacter);
            return (VisualSpan.Character)span;
        }

        public static VisualSpan.Line AsLine(this VisualSpan span)
        {
            Assert.True(span.IsLine);
            return (VisualSpan.Line)span;
        }

        public static VisualSpan.Block AsBlock(this VisualSpan span)
        {
            Assert.True(span.IsBlock);
            return (VisualSpan.Block)span;
        }

        #endregion

        #region VisualSelection

        public static VisualSelection.Character AsCharacter(this VisualSelection span)
        {
            Assert.True(span.IsCharacter);
            return (VisualSelection.Character)span;
        }

        public static VisualSelection.Line AsLine(this VisualSelection span)
        {
            Assert.True(span.IsLine);
            return (VisualSelection.Line)span;
        }

        public static VisualSelection.Block AsBlock(this VisualSelection span)
        {
            Assert.True(span.IsBlock);
            return (VisualSelection.Block)span;
        }

        #endregion

        #region ICommandRunner

        public static BindResult<CommandRunData> Run(this ICommandRunner runner, VimKey key)
        {
            return runner.Run(KeyInputUtil.VimKeyToKeyInput(key));
        }

        public static BindResult<CommandRunData> Run(this ICommandRunner runner, char c)
        {
            return runner.Run(KeyInputUtil.CharToKeyInput(c));
        }

        /// <summary>
        /// Run the multi-input command
        /// </summary>
        public static BindResult<CommandRunData> Run(this ICommandRunner runner, string command)
        {
            BindResult<CommandRunData> result = null;
            for (var i = 0; i < command.Length; i++)
            {
                result = runner.Run(command[i]);
                if (i + 1 < command.Length)
                {
                    Assert.True(result.IsNeedMoreInput, "Needs more input");
                }
            }

            return result;
        }

        #endregion

        #region ICommonOperations

        public static void ShiftLineRangeLeft(this ICommonOperations operations, int count)
        {
            var number = operations.TextView.GetCaretLine().LineNumber;
            var range = operations.TextView.GetLineRange(number, number + (count - 1));
            operations.ShiftLineRangeLeft(range, 1);
        }

        public static void ShiftLineRangeRight(this ICommonOperations operations, int count)
        {
            var number = operations.TextView.GetCaretLine().LineNumber;
            var range = operations.TextView.GetLineRange(number, number + (count - 1));
            operations.ShiftLineRangeRight(range, 1);
        }

        #endregion

        #region BindResult<T>

        public static BindResult<T> Run<T>(this BindResult<T> result, string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(text[i]);
                Assert.True(result.IsNeedMoreInput);
                result = result.AsNeedMoreInput().Item.BindFunction.Invoke(keyInput);
            }

            return result;
        }

        public static BindResult<T> Run<T>(this BindResult<T> result, params VimKey[] keys)
        {
            foreach (var cur in keys)
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.True(result.IsNeedMoreInput);
                result = result.AsNeedMoreInput().Item.BindFunction.Invoke(keyInput);
            }
            return result;
        }

        #endregion

        #region BindData<T>

        public static BindResult<T> Run<T>(this BindData<T> data, string text)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(text[0]);
            return data.BindFunction.Invoke(keyInput).Run(text.Substring(1));
        }

        public static BindResult<T> Run<T>(this BindData<T> data, params VimKey[] keys)
        {
            var result = data.BindFunction.Invoke(KeyInputUtil.VimKeyToKeyInput(keys[0]));
            return result.Run(keys.Skip(1).ToArray());
        }

        #endregion

        #region SearchResult

        public static SearchResult.Found AsFound(this SearchResult result)
        {
            Assert.True(result.IsFound);
            return (SearchResult.Found)result;
        }

        public static SearchResult.NotFound AsNotFound(this SearchResult result)
        {
            Assert.True(result.IsNotFound);
            return (SearchResult.NotFound)result;
        }

        public static bool IsFound(this SearchResult result, int startPosition)
        {
            return result.IsFound && result.AsFound().Item2.Start == startPosition;
        }

        #endregion

        #region CaretColumn

        public static CaretColumn.InLastLine AsInLastLine(this CaretColumn column)
        {
            return (CaretColumn.InLastLine)column;
        }

        #endregion

        #region IIncrementalSearch

        public static BindResult<SearchResult> DoSearch(this IIncrementalSearch search, string text, Path path = null, bool enter = true)
        {
            path = path ?? Path.Forward;
            var result = search.Begin(path).Run(text);
            return enter
                ? result.Run(VimKey.Enter)
                : result;
        }

        #endregion

        #region TextChange

        public static TextChange.Insert AsInsert(this TextChange change)
        {
            return (TextChange.Insert)change;
        }

        public static TextChange.DeleteLeft AsDeleteLeft(this TextChange change)
        {
            return (TextChange.DeleteLeft)change;
        }

        public static TextChange.DeleteRight AsDeleteRight(this TextChange change)
        {
            return (TextChange.DeleteRight)change;
        }

        public static TextChange.Combination AsCombination(this TextChange change)
        {
            return (TextChange.Combination)change;
        }

        public static bool IsInsert(this TextChange change, string text)
        {
            return change.IsInsert && change.AsInsert().Item == text;
        }

        public static bool IsDeleteLeft(this TextChange change, int count)
        {
            return change.IsDeleteLeft && change.AsDeleteLeft().Item == count;
        }

        public static bool IsDeleteRight(this TextChange change, int count)
        {
            return change.IsDeleteRight && change.AsDeleteRight().Item == count;
        }

        #endregion

        #region SnapshotSpan

        /// <summary>
        /// Convert the SnapshotSpan into an EditSpan
        /// </summary>
        public static EditSpan ToEditSpan(this SnapshotSpan span)
        {
            return EditSpan.NewSingle(span);
        }

        #endregion

        #region IMarkMap

        public static void SetLocalMark(this IMarkMap markMap, char c, IVimBufferData vimBufferData, int line, int column)
        {
            var letter = Letter.OfChar(c).Value;
            var mark = Mark.NewLocalMark(LocalMark.NewLetter(letter));
            markMap.SetMark(mark, vimBufferData, line, column);
        }

        #endregion

        #region VisualElement

        public static TextComposition CreateTextComposition(this FrameworkElement frameworkElement, string text, InputManager inputManager = null)
        {
            inputManager = inputManager ?? InputManager.Current;
            var textComposition = new TextComposition(inputManager, frameworkElement, text);
            if (text.Length == 1)
            {
                var c = text[0];
                if (Char.IsControl(c))
                {
                    var type = typeof(TextComposition);
                    var method = type.GetMethod("MakeControl", BindingFlags.Instance | BindingFlags.NonPublic);
                    method.Invoke(textComposition, new object[] { });
                    Assert.True(String.IsNullOrEmpty(textComposition.Text));
                    Assert.Equal(text, textComposition.ControlText);
                }
                else if (0 != (c & 0x80))
                {
                    var type = typeof(TextComposition);
                    var method = type.GetMethod("MakeSystem", BindingFlags.Instance | BindingFlags.NonPublic);
                    method.Invoke(textComposition, new object[] { });
                    Assert.True(String.IsNullOrEmpty(textComposition.Text));
                    Assert.Equal(text, textComposition.SystemText);
                }
            }

            return textComposition;
        }

        public static TextCompositionEventArgs CreateTextCompositionEventArgs(this FrameworkElement frameworkElement, string text, InputDevice inputDevice, InputManager inputManager = null)
        {
            var textComposition = CreateTextComposition(frameworkElement, text, inputManager);
            var args = new TextCompositionEventArgs(inputDevice, textComposition);
            args.RoutedEvent = UIElement.TextInputEvent;
            return args;
        }

        #endregion

        #region HistoryList

        public static void AddRange(this HistoryList historyList, params string[] values)
        {
            foreach (var cur in values)
            {
                historyList.Add(cur);
            }
        }

        #endregion

        /// <summary>
        /// Run the specified motion with default arguments
        /// </summary>
        public static FSharpOption<MotionResult> GetMotion(this IMotionUtil motionUtil, Motion motion)
        {
            var arg = new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            return motionUtil.GetMotion(motion, arg);
        }

        public static SnapshotSpan GetSpan(this ITextSelection selection)
        {
            var span = new SnapshotSpan(selection.Start.Position, selection.End.Position);
            return span;
        }

        public static void Select(this ITextSelection selection, int start, int length)
        {
            var snapshotSpan = new SnapshotSpan(selection.TextView.TextSnapshot, start, length);
            selection.Select(snapshotSpan);
        }

        public static void Select(this ITextSelection selection, SnapshotPoint startPoint, SnapshotPoint endPoint)
        {
            selection.Select(new VirtualSnapshotPoint(startPoint), new VirtualSnapshotPoint(endPoint));
        }

        public static void Select(this ITextSelection selection, params SnapshotSpan[] spans)
        {
            if (spans.Length == 1)
            {
                selection.Mode = TextSelectionMode.Stream;
                selection.Clear();
                selection.Select(spans[0], false);
            }
            else
            {
                selection.Mode = TextSelectionMode.Box;
                foreach (var span in spans)
                {
                    selection.Select(span, false);
                }
            }
        }

        public static void UpdateValue(this Register reg, string value)
        {
            UpdateValue(reg, value, OperationKind.CharacterWise);
        }

        /// <summary>
        /// Update the value with the string followed by the specified VimKey values
        /// </summary>
        public static void UpdateValue(this Register reg, string value, params VimKey[] keys)
        {
            var left = value.Select(KeyInputUtil.CharToKeyInput);
            var right = keys.Select(KeyInputUtil.VimKeyToKeyInput);
            var all = left.Concat(right).ToFSharpList();
            reg.RegisterValue = RegisterValue.NewKeyInput(all, OperationKind.CharacterWise);
        }

        /// <summary>
        /// Update the value with the spcefied set of VimKey values
        /// </summary>
        public static void UpdateValue(this Register reg, params VimKey[] keys)
        {
            var all = keys.Select(KeyInputUtil.VimKeyToKeyInput).ToFSharpList();
            reg.RegisterValue = RegisterValue.NewKeyInput(all, OperationKind.CharacterWise);
        }

        /// <summary>
        /// Update the value with the specified set of KeyInput values
        /// </summary>
        public static void UpdateValue(this Register reg, params KeyInput[] keys)
        {
            reg.RegisterValue = RegisterValue.NewKeyInput(keys.ToFSharpList(), OperationKind.CharacterWise);
        }

        public static void UpdateValue(this Register reg, string value, OperationKind kind)
        {
            reg.RegisterValue = RegisterValue.OfString(value, kind);
        }

        public static void UpdateBlockValues(this Register reg, params string[] value)
        {
            var col = NonEmptyCollectionUtil.OfSeq(value).Value;
            var data = StringData.NewBlock(col);
            reg.RegisterValue = RegisterValue.NewString(data, OperationKind.CharacterWise);
        }

        public static VirtualSnapshotPoint GetCaretVirtualPoint(this ITextView view)
        {
            return view.Caret.Position.VirtualBufferPosition;
        }

        public static SnapshotSpan GetSelectionSpan(this ITextView textView)
        {
            return textView.Selection.StreamSelectionSpan.SnapshotSpan;
        }

        public static BlockSpan GetSelectionBlockSpan(this ITextView textView)
        {
            Assert.Equal(TextSelectionMode.Box, textView.Selection.Mode);
            var spans = textView.Selection.SelectedSpans;
            var first = spans[0];
            return new BlockSpan(first.Start, first.Length, spans.Count);
        }

        public static Register GetRegister(this IRegisterMap map, char c)
        {
            var name = RegisterNameUtil.CharToRegister(c).Value;
            return map.GetRegister(name);
        }

        public static bool IsSome<T>(this FSharpOption<T> option, T value)
        {
            Assert.True(option.IsSome(), "Option is None");
            Assert.Equal(value, option.Value);
            return true;
        }

        public static bool IsSome<T>(this FSharpOption<T> option, Func<T, bool> func)
        {
            Assert.True(option.IsSome());
            Assert.True(func(option.Value));
            return true;
        }

        public static int GetColumn(this SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            return point.Position - line.Start.Position;
        }
    }
}
