using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim.Extensions;
using Vim.Modes.Command;

namespace Vim.UnitTest
{
    public static class Extensions
    {
        #region CommandResult

        public static CommandResult.Completed AsCompleted(this CommandResult result)
        {
            Assert.IsTrue(result.IsCompleted);
            return (CommandResult.Completed)result;
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

        #region ParseRangeResult

        internal static ParseRangeResult.Succeeded AsSucceeded(this ParseRangeResult res)
        {
            return (ParseRangeResult.Succeeded)res;
        }

        internal static ParseRangeResult.Failed AsFailed(this ParseRangeResult res)
        {
            return (ParseRangeResult.Failed)res;
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

        #region IVimBuffer

        public static bool CanProcess(this IVimBuffer buffer, char c)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            return buffer.CanProcess(keyInput);
        }

        public static bool CanProcess(this IVimBuffer buffer, VimKey key)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(key);
            return buffer.CanProcess(keyInput);
        }

        public static bool Process(this IVimBuffer buf, VimKey key)
        {
            return buf.Process(KeyInputUtil.VimKeyToKeyInput(key)).IsAnyHandled;
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

        public static Register GetRegister(this IVimBuffer buffer, char c)
        {
            var name = RegisterNameUtil.CharToRegister(c).Value;
            return buffer.RegisterMap.GetRegister(name);
        }

        #endregion

        #region ITextView

        public static SnapshotPoint GetPoint(this ITextView textView, int position)
        {
            return new SnapshotPoint(textView.TextSnapshot, position);
        }

        public static SnapshotPoint GetPointInLine(this ITextView textView, int line, int column)
        {
            return textView.TextBuffer.GetPointInLine(line, column);
        }

        public static SnapshotPoint GetEndPoint(this ITextView textView)
        {
            return textView.TextSnapshot.GetEndPoint();
        }

        public static ITextSnapshotLine GetLine(this ITextView textView, int line)
        {
            return textView.TextSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotLineRange GetLineRange(this ITextView textView, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRangeUtil.CreateForLineNumberRange(textView.TextSnapshot, startLine, endLine);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int length)
        {
            return GetLineSpan(textView, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int column, int length)
        {
            return GetLineSpan(textView.TextBuffer, lineNumber, column, length);
        }

        public static ITextSnapshotLine GetLastLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetLastLine();
        }

        public static ITextSnapshotLine GetFirstLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetFirstLine();
        }

        public static CaretPosition MoveCaretTo(this ITextView textView, int position)
        {
            return textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, position));
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber)
        {
            return MoveCaretTo(textView, textView.GetLine(lineNumber).Start.Position);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber, int column)
        {
            var point = textView.GetLine(lineNumber).Start.Add(column);
            return MoveCaretTo(textView, point.Position);
        }

        /// <summary>
        /// Change the selection to be the specified SnapshotSpan value and update the caret to be on the
        /// last included point in the SnapshotSpan.  
        /// </summary>
        public static void SelectAndUpdateCaret(this ITextView textView, SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            textView.Selection.Mode = mode;

            // The editor will normalize SnapshotSpan values here which extend into the line break
            // portion of the line to not include the line break.  Must use VirtualSnapshotPoint 
            // values to ensure the proper selection
            var startPoint = span.Start.ToVirtualSnapshotPoint();
            var endPoint = span.End.ToVirtualSnapshotPoint();
            textView.Selection.Select(startPoint, endPoint);
            var point = span.Length > 0 ? span.End.Subtract(1) : span.Start;
            MoveCaretTo(textView, point);
        }

        public static ITextSnapshotLine GetCaretLine(this ITextView textView)
        {
            return textView.Caret.Position.BufferPosition.GetContainingLine();
        }

        public static void SetText(this ITextView textView, params string[] lines)
        {
            SetText(textView.TextBuffer, lines);
        }

        public static void SetText(this ITextView textView, string text, int? caret = null)
        {
            SetText(textView.TextBuffer, text);
            if (caret.HasValue)
            {
                MoveCaretTo(textView, caret.Value);
            }
        }

        public static VisualSpan GetVisualSpanBlock(this ITextView textView, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return GetVisualSpanBlock(textView.TextBuffer, column, length, startLine, lineCount);
        }

        public static NonEmptyCollection<SnapshotSpan> GetBlock(this ITextView textView, int column, int length, int startLine = 0, int lineCount = 1)
        {
            return GetBlock(textView.TextBuffer, column, length, startLine, lineCount);
        }

        #endregion

        #region ITextBuffer

        public static ITextSnapshotLine GetLineFromLineNumber(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        public static ITextSnapshotLine GetLine(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotLineRange GetLineRange(this ITextBuffer buffer, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRangeUtil.CreateForLineNumberRange(buffer.CurrentSnapshot, startLine, endLine);
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

        public static SnapshotPoint GetPoint(this ITextBuffer buffer, int position)
        {
            return new SnapshotPoint(buffer.CurrentSnapshot, position);
        }

        public static SnapshotPoint GetPointInLine(this ITextBuffer textBuffer, int line, int column)
        {
            var snapshotLine = textBuffer.GetLine(line);
            return snapshotLine.Start.Add(column);
        }

        public static SnapshotPoint GetEndPoint(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetEndPoint();
        }

        public static SnapshotSpan GetExtent(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetExtent();
        }

        public static SnapshotSpan GetSpan(this ITextBuffer buffer, int start, int length)
        {
            return buffer.CurrentSnapshot.GetSpan(start, length);
        }

        public static void SetText(this ITextBuffer buffer, params string[] lines)
        {
            var text = lines.Aggregate((x, y) => x + Environment.NewLine + y);
            var edit = buffer.CreateEdit(EditOptions.DefaultMinimalChange, 0, null);
            edit.Replace(new Span(0, buffer.CurrentSnapshot.Length), text);
            edit.Apply();
        }

        public static NonEmptyCollection<SnapshotSpan> GetBlock(this ITextBuffer textBuffer, int column, int length, int startLine = 0, int lineCount = 1)
        {
            var list = new List<SnapshotSpan>();
            for (var i = 0; i < lineCount; i++)
            {
                list.Add(textBuffer.GetLineSpan(i + startLine, column: column, length: length));
            }

            return NonEmptyCollectionUtil.OfSeq(list).Value;
        }

        public static VisualSpan GetVisualSpanBlock(this ITextBuffer textBuffer, int column, int length, int startLine = 0, int lineCount = 1)
        {
            var col = GetBlock(textBuffer, column, length, startLine, lineCount);
            return VisualSpan.NewBlock(col);
        }

        #endregion

        #region ITextSnapshot

        public static ITextSnapshotLine GetLine(this ITextSnapshot tss, int lineNumber)
        {
            return tss.GetLineFromLineNumber(lineNumber);
        }

        public static SnapshotLineRange GetLineRange(this ITextSnapshot tss, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRangeUtil.CreateForLineNumberRange(tss, startLine, endLine);
        }

        public static ITextSnapshotLine GetFirstLine(this ITextSnapshot tss)
        {
            return GetLine(tss, 0);
        }

        public static ITextSnapshotLine GetLastLine(this ITextSnapshot tss)
        {
            return GetLine(tss, tss.LineCount - 1);
        }

        public static SnapshotPoint GetPoint(this ITextSnapshot tss, int position)
        {
            return new SnapshotPoint(tss, position);
        }

        public static SnapshotPoint GetEndPoint(this ITextSnapshot tss)
        {
            return new SnapshotPoint(tss, tss.Length);
        }

        public static SnapshotSpan GetSpan(this ITextSnapshot tss, int start, int length)
        {
            return new SnapshotSpan(tss, start, length);
        }

        public static SnapshotSpan GetExtent(this ITextSnapshot snapshot)
        {
            return new SnapshotSpan(snapshot, 0, snapshot.Length);
        }

        public static NormalizedSnapshotSpanCollection GetTaggerExtent(this ITextSnapshot snapshot)
        {
            var span = GetExtent(snapshot);
            return new NormalizedSnapshotSpanCollection(span);
        }

        #endregion

        #region SnapshotPoint

        public static SnapshotSpan GetSpan(this SnapshotPoint point, int length)
        {
            return new SnapshotSpan(point, length);
        }

        /// <summary>
        /// Convert the SnapshotPoint into a VirtualSnapshotPoint taking into account the editors
        /// view that SnapshotPoint values in the line break should be represented as 
        /// VirtualSnapshotPoint values
        /// </summary>
        public static VirtualSnapshotPoint ToVirtualSnapshotPoint(this SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            var difference = point.Position - line.End.Position;
            if (difference > 0)
            {
                return new VirtualSnapshotPoint(line.End, difference);
            }

            return new VirtualSnapshotPoint(point);
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
                    Assert.IsTrue(result.IsNeedMoreInput);
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

        #region Dispatcher

        public static void DoEvents(this System.Windows.Threading.Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            Action<DispatcherFrame> action = _ => { frame.Continue = false; };
            dispatcher.BeginInvoke(
                DispatcherPriority.SystemIdle,
                action,
                frame);
            Dispatcher.PushFrame(frame);
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

        public static TextChange.Delete AsDelete(this TextChange change)
        {
            return (TextChange.Delete)change;
        }

        public static TextChange.Combination AsCombination(this TextChange change)
        {
            return (TextChange.Combination)change;
        }

        public static bool IsInsert(this TextChange change, string text)
        {
            return change.IsInsert && change.AsInsert().Item == text;
        }

        public static bool IsDelete(this TextChange change, int count)
        {
            return change.IsDelete && change.AsDelete().Item == count;
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
