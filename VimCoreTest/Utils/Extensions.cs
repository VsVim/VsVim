using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using VimCore;
using VimCore.Modes.Common;

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

        #region RangeResult

        internal static VimCore.Modes.Command.RangeResult.Range AsRange(this VimCore.Modes.Command.RangeResult res)
        {
            return (VimCore.Modes.Command.RangeResult.Range)res;
        }

        internal static VimCore.Modes.Command.RangeResult.NeedMore AsNeedMore(this VimCore.Modes.Command.RangeResult res)
        {
            return (VimCore.Modes.Command.RangeResult.NeedMore)res;
        }

        internal static VimCore.Modes.Command.RangeResult.Empty AsEmpty(this VimCore.Modes.Command.RangeResult res)
        {
            return (VimCore.Modes.Command.RangeResult.Empty)res;
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
