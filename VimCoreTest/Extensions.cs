using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Modes;

namespace VimCore.Test
{
    internal static class Extensions
    {
        #region CountResult

        internal static CountResult.NeedMore AsNeedMore(this CountResult res)
        {
            return (CountResult.NeedMore)res;
        }

        #endregion

        #region ProcessResult

        internal static ProcessResult.SwitchMode AsSwitchMode(this ProcessResult res)
        {
            return (ProcessResult.SwitchMode)res;
        }

        internal static ProcessResult.SwitchModeWithArgument AsSwitchModeWithArgument(this ProcessResult res)
        {
            return (ProcessResult.SwitchModeWithArgument)res;
        }

        #endregion

        #region MotionResult


        internal static MotionResult.Complete AsComplete(this MotionResult res)
        {
            Assert.IsTrue(res.IsComplete);
            return (MotionResult.Complete)res;
        }

        #endregion

        #region ModeUtil.Result

        internal static Result.Failed AsFailed(this Result res)
        {
            return (Result.Failed)res;
        }

        #endregion

        #region ParseRangeResult


        internal static Vim.Modes.Command.ParseRangeResult.Succeeded AsSucceeded(this Vim.Modes.Command.ParseRangeResult res)
        {
            return (Vim.Modes.Command.ParseRangeResult.Succeeded)res;
        }

        internal static Vim.Modes.Command.ParseRangeResult.Failed AsFailed(this Vim.Modes.Command.ParseRangeResult res)
        {
            return (Vim.Modes.Command.ParseRangeResult.Failed)res;
        }

        #endregion

        #region IKeyMap

        internal static IEnumerable<KeyInput> GetKeyMapping(this IKeyMap keyMap, KeyInput ki, KeyRemapMode mode)
        {
            var set = KeyInputSet.NewOneKeyInput(ki);
            return keyMap.GetKeyMapping(set, mode).AsMapped().Item.KeyInputs;
        }

        internal static KeyMappingResult GetKeyMappingResult(this IKeyMap keyMap, KeyInput ki, KeyRemapMode mode)
        {
            var set = KeyInputSet.NewOneKeyInput(ki);
            return keyMap.GetKeyMapping(set, mode);
        }

        #endregion

        #region KeyMappingResult

        internal static Vim.KeyMappingResult.Mapped AsMapped(this KeyMappingResult res)
        {
            Assert.IsTrue(res.IsMapped);
            return (KeyMappingResult.Mapped)res;
        }

        internal static Vim.KeyMappingResult.RecursiveMapping AsRecursiveMapping(this KeyMappingResult res)
        {
            Assert.IsTrue(res.IsRecursiveMapping);
            return (KeyMappingResult.RecursiveMapping)res;
        }

        #endregion

        #region SearchText

        internal static Vim.SearchText.Pattern AsPattern(this SearchText text)
        {
            Assert.IsTrue(text.IsPattern);
            return (SearchText.Pattern)text;
        }

        internal static Vim.SearchText.StraightText AsStraightText(this SearchText text)
        {
            Assert.IsTrue(text.IsStraightText);
            return (SearchText.StraightText)text;
        }

        internal static Vim.SearchText.WholeWord AsWholeWord(this SearchText text)
        {
            Assert.IsTrue(text.IsWholeWord);
            return (SearchText.WholeWord)text;
        }

        #endregion

        #region RepeatableChange

        internal static RepeatableChange.TextChange AsTextChange(this RepeatableChange change)
        {
            Assert.IsTrue(change.IsTextChange);
            return (RepeatableChange.TextChange)change;
        }

        internal static RepeatableChange.CommandChange AsCommandChange(this RepeatableChange change)
        {
            Assert.IsTrue(change.IsCommandChange);
            return (RepeatableChange.CommandChange)change;
        }

        #endregion

        #region SettingValue

        internal static SettingValue.StringValue AsStringValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsStringValue);
            return (SettingValue.StringValue)value;
        }

        internal static SettingValue.ToggleValue AsBooleanValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsToggleValue);
            return (SettingValue.ToggleValue)value;
        }

        internal static SettingValue.NumberValue AsNumberValue(this SettingValue value)
        {
            Assert.IsTrue(value.IsNumberValue);
            return (SettingValue.NumberValue)value;
        }

        #endregion

        #region Range

        internal static Vim.Modes.Command.Range.Lines AsLines(this Vim.Modes.Command.Range range)
        {
            return (Vim.Modes.Command.Range.Lines)range;
        }

        internal static Vim.Modes.Command.Range.RawSpan AsRawSpan(this Vim.Modes.Command.Range range)
        {
            return (Vim.Modes.Command.Range.RawSpan)range;
        }

        internal static Vim.Modes.Command.Range.SingleLine AsSingleLine(this Vim.Modes.Command.Range range)
        {
            return (Vim.Modes.Command.Range.SingleLine)range;
        }

        #endregion

        #region RunKeyInputResult

        public static RunKeyInputResult.CommandRan AsCommandRan(this RunKeyInputResult result)
        {
            return (RunKeyInputResult.CommandRan)result;
        }

        public static RunKeyInputResult.CommandErrored AsCommandErrored(this RunKeyInputResult result)
        {
            return (RunKeyInputResult.CommandErrored)result;
        }

        #endregion

        #region IMode

        public static bool CanProcess(this IMode mode, VimKey key)
        {
            return mode.CanProcess(InputUtil.VimKeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, VimKey key)
        {
            return mode.Process(InputUtil.VimKeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, char c)
        {
            return mode.Process((InputUtil.CharToKeyInput(c)));
        }

        public static ProcessResult Process(this IMode mode, string input)
        {
            ProcessResult last = null;
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                last = mode.Process(c);
            }

            return last;
        }

        #endregion

        #region IVimBuffer

        public static bool ProcessChar(this IVimBuffer buf, char c)
        {
            return buf.Process(InputUtil.CharToKeyInput(c));
        }

        public static void ProcessAsString(this IVimBuffer buf, string input)
        {
            ProcessInputAsString(buf, input);
        }

        public static void ProcessInputAsString(this IVimBuffer buf, string input)
        {
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                buf.Process(i);
            }
        }

        #endregion

        #region ITextView 

        public static SnapshotPoint GetPoint(this ITextView textView, int position)
        {
            return new SnapshotPoint(textView.TextSnapshot, position);
        }

        public static ITextSnapshotLine GetLine(this ITextView textView, int line)
        {
            return textView.TextSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotSpan GetLineSpanIncludingLineBreak(this ITextView textView, int startLine, int endLine=-1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return textView.TextSnapshot.GetLineSpanIncludingLineBreak(startLine, endLine);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int startLine, int endLine=-1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return textView.TextSnapshot.GetLineSpan(startLine, endLine);
        }

        public static CaretPosition MoveCaretTo(this ITextView textView, int position)
        {
            return textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, position));
        }

        public static ITextSnapshotLine GetCaretLine(this ITextView textView)
        {
            return textView.Caret.Position.BufferPosition.GetContainingLine();
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

        public static SnapshotSpan GetLineSpanIncludingLineBreak(this ITextBuffer buffer, int startLine, int endLine=-1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return buffer.CurrentSnapshot.GetLineSpanIncludingLineBreak(startLine, endLine);
        }

        public static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return buffer.CurrentSnapshot.GetLineSpan(startLine, endLine);
        }

        public static SnapshotPoint GetPoint(this ITextBuffer buffer, int position)
        {
            return new SnapshotPoint(buffer.CurrentSnapshot, position);
        }

        public static SnapshotSpan GetSpan(this ITextBuffer buffer, int start, int length)
        {
            return buffer.CurrentSnapshot.GetSpan(start, length);
        }

        #endregion

        #region ITextSnapshot


        public static ITextSnapshotLine GetLine(this ITextSnapshot tss, int lineNumber)
        {
            return tss.GetLineFromLineNumber(lineNumber);
        }

        public static SnapshotSpan GetLineSpan(this ITextSnapshot tss, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            var start = tss.GetLineFromLineNumber(startLine);
            var end = tss.GetLineFromLineNumber(endLine);
            return new SnapshotSpan(start.Start, end.End);
        }

        public static SnapshotSpan GetLineSpanIncludingLineBreak(this ITextSnapshot tss, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            var start = tss.GetLineFromLineNumber(startLine);
            var end = tss.GetLineFromLineNumber(endLine);
            return new SnapshotSpan(start.Start, end.EndIncludingLineBreak);
        }

        public static SnapshotPoint GetPoint(this ITextSnapshot tss, int position)
        {
            return new SnapshotPoint(tss, position);
        }

        public static SnapshotSpan GetSpan(this ITextSnapshot tss, int start, int length)
        {
            return new SnapshotSpan(tss, start, length);
        }

        #endregion

        #region ICommandRunner

        internal static RunKeyInputResult Run(this ICommandRunner runner, char c)
        {
            return runner.Run(InputUtil.CharToKeyInput(c));
        }

        #endregion

        #region CommandRunnerState

        internal static CommandRunnerState.NotFinishWithCommand AsNotFinishedWithCommand(this CommandRunnerState state)
        {
            return (CommandRunnerState.NotFinishWithCommand)state;
        }

        internal static CommandRunnerState.NotEnoughMatchingPrefix AsNotEnoughMatchingPrefix(this CommandRunnerState state)
        {
            return (CommandRunnerState.NotEnoughMatchingPrefix)state;
        }

        #endregion

        internal static SnapshotSpan GetSpan(this ITextSelection selection)
        {
            var span = new SnapshotSpan(selection.Start.Position, selection.End.Position);
            return span;
        }

        internal static void UpdateValue(this Register reg, string value)
        {
            var regValue = new RegisterValue(value, MotionKind.Inclusive, OperationKind.CharacterWise);
            reg.UpdateValue(regValue);
        }

        internal static SnapshotPoint GetCaretPoint(this ITextView view)
        {
            return view.Caret.Position.BufferPosition;
        }

        internal static void DoEvents(this System.Windows.Threading.Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            Action<DispatcherFrame> action = _ => { frame.Continue = false; };
            dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                action,
                frame);
            Dispatcher.PushFrame(frame);

        }

    }
}
