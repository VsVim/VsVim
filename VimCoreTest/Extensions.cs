﻿using System;
using System.Collections.Generic;
using System.Linq;
using EditorUtils.UnitTest;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim.Extensions;
using Vim.Interpreter;

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
            Assert.IsTrue(result.IsCompleted);
            return (CommandResult.Completed)result;
        }

        #endregion

        #region VisualSelection

        public static void SelectAndMoveCaret(this VisualSelection selection, ITextView textView)
        {
            selection.SelectAndMoveCaret(textView, SelectionKind.Inclusive);
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

        #region Value

        /// <summary>
        /// Number version of a value
        /// </summary>
        public static Value.Number AsNumber(this Value value)
        {
            return (Value.Number)value;
        }

        /// <summary>
        /// String version of a value
        /// </summary>
        public static Value.String AsString(this Value value)
        {
            return (Value.String)value;
        }

        #endregion

        #region ModeSwitch

        public static ModeSwitch.SwitchModeWithArgument AsSwitchModeWithArgument(this ModeSwitch mode)
        {
            Assert.IsTrue(mode.IsSwitchModeWithArgument);
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
            Assert.IsTrue(res.IsComplete);
            return (BindResult<T>.Complete)res;
        }

        public static BindResult<T>.NeedMoreInput AsNeedMoreInput<T>(this BindResult<T> res)
        {
            Assert.IsTrue(res.IsNeedMoreInput);
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
            Assert.IsTrue(command.IsVisualCommand);
            return (Command.VisualCommand)command;
        }

        public static Command.NormalCommand AsNormalCommand(this Command command)
        {
            Assert.IsTrue(command.IsNormalCommand);
            return (Command.NormalCommand)command;
        }

        public static Command.InsertCommand AsInsertCommand(this Command command)
        {
            Assert.IsTrue(command.IsInsertCommand);
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
            Assert.IsTrue(res.IsMapped);
            return (KeyMappingResult.Mapped)res;
        }

        #endregion

        #region SettingValue

        public static SettingValue.StringValue AsStringValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsStringValue);
            return (SettingValue.StringValue)value;
        }

        public static SettingValue.ToggleValue AsToggleValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsToggleValue);
            return (SettingValue.ToggleValue)value;
        }

        public static SettingValue.NumberValue AsNumberValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsNumberValue);
            return (SettingValue.NumberValue)value;
        }

        #endregion

        #region RunResult

        public static RunResult.SubstituteConfirm AsSubstituteConfirm(this RunResult result)
        {
            Assert.IsTrue(result.IsSubstituteConfirm);
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

        #endregion

        #region IVimBufferFactory

        public static IVimBuffer CreateVimBuffer(this IVimBufferFactory vimBufferFactory, ITextView textView, IVimTextBuffer vimTextBuffer)
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
        public static void ProcessNotation(this IVimBuffer vimBuffer, string notation)
        {
            var keyInputSet = KeyNotationUtil.StringToKeyInputSet(notation);
            foreach (var keyInput in keyInputSet.KeyInputs)
            {
                vimBuffer.Process(keyInput);
            }
        }

        public static Register GetRegister(this IVimBuffer buffer, char c)
        {
            var name = RegisterNameUtil.CharToRegister(c).Value;
            return buffer.RegisterMap.GetRegister(name);
        }

        #endregion

        #region ITextBuffer

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

        #endregion

        #region ITextView

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

        #endregion

        #region VisualSpan

        public static VisualSpan.Character AsCharacter(this VisualSpan span)
        {
            Assert.IsTrue(span.IsCharacter);
            return (VisualSpan.Character)span;
        }

        public static VisualSpan.Line AsLine(this VisualSpan span)
        {
            Assert.IsTrue(span.IsLine);
            return (VisualSpan.Line)span;
        }

        public static VisualSpan.Block AsBlock(this VisualSpan span)
        {
            Assert.IsTrue(span.IsBlock);
            return (VisualSpan.Block)span;
        }

        #endregion

        #region VisualSelection

        public static VisualSelection.Character AsCharacter(this VisualSelection span)
        {
            Assert.IsTrue(span.IsCharacter);
            return (VisualSelection.Character)span;
        }

        public static VisualSelection.Line AsLine(this VisualSelection span)
        {
            Assert.IsTrue(span.IsLine);
            return (VisualSelection.Line)span;
        }

        public static VisualSelection.Block AsBlock(this VisualSelection span)
        {
            Assert.IsTrue(span.IsBlock);
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
                    Assert.IsTrue(result.IsNeedMoreInput, "Needs more input");
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
                Assert.IsTrue(result.IsNeedMoreInput);
                result = result.AsNeedMoreInput().Item.BindFunction.Invoke(keyInput);
            }

            return result;
        }

        public static BindResult<T> Run<T>(this BindResult<T> result, params VimKey[] keys)
        {
            foreach (var cur in keys)
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.IsTrue(result.IsNeedMoreInput);
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
            Assert.IsTrue(result.IsFound);
            return (SearchResult.Found)result;
        }

        public static SearchResult.NotFound AsNotFound(this SearchResult result)
        {
            Assert.IsTrue(result.IsNotFound);
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

        public static SnapshotPoint GetCaretPoint(this ITextView view)
        {
            return view.Caret.Position.BufferPosition;
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
            Assert.AreEqual(TextSelectionMode.Box, textView.Selection.Mode);
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
            Assert.IsTrue(option.IsSome(), "Option is None");
            Assert.AreEqual(value, option.Value);
            return true;
        }

        public static bool IsSome<T>(this FSharpOption<T> option, Func<T, bool> func)
        {
            Assert.IsTrue(option.IsSome());
            Assert.IsTrue(func(option.Value));
            return true;
        }
    }
}
