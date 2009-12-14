using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.Modes.Common;

namespace VimCoreTest.Utils
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

        #endregion

        #region MotionResult


        internal static MotionResult.Complete AsComplete(this MotionResult res)
        {
            return (MotionResult.Complete)res;
        }

        internal static MotionResult.InvalidMotion AsInvalidMotion(this MotionResult res)
        {
            return (MotionResult.InvalidMotion)res;
        }

        #endregion

        #region Operations.Result

        internal static Operations.Result.Failed AsFailed(this Operations.Result res)
        {
            return (Operations.Result.Failed)res;
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

        #region Range

        internal static Vim.Modes.Command.Range.Lines AsLines(this Vim.Modes.Command.Range range)
        {
            return (Vim.Modes.Command.Range.Lines)range;
        }

        internal static Vim.Modes.Command.Range.RawSpan AsRawSpan(this Vim.Modes.Command.Range range)
        {
            return (Vim.Modes.Command.Range.RawSpan)range;
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

    }
}
