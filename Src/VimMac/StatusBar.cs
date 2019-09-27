using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;
namespace Vim.Mac
{
    // Mostly copied from Vim.UI.Wpf.Implementation.CommandMargin.CommandMarginUtil
    internal static class StatusBar
    {
        internal static bool InPasteWait(IVimBuffer vimBuffer)
        {
            if (vimBuffer.ModeKind == ModeKind.Command)
            {
                return vimBuffer.CommandMode.InPasteWait;
            }

            var search = vimBuffer.IncrementalSearch;
            if (search.HasActiveSession && search.InPasteWait)
            {
                return true;
            }

            return false;
        }

        internal static EditableCommand GetStatus(IVimBuffer vimBuffer)
        {
            return GetStatus(vimBuffer, vimBuffer.Mode, forModeSwitch: false);
        }

        internal static EditableCommand GetStatus(IVimBuffer vimBuffer, IMode currentMode, bool forModeSwitch)
        {
            if (forModeSwitch)
            {
                return GetStatusCommon(vimBuffer, currentMode);
            }

            return GetStatusOther(vimBuffer, currentMode);
        }

        private static EditableCommand GetStatusOther(IVimBuffer vimBuffer, IMode currentMode)
        {
            var search = vimBuffer.IncrementalSearch;
            if (search.HasActiveSession)
            {
                var searchText = search.CurrentSearchText;
                var prefix = search.CurrentSearchData.Path.IsForward ? "/" : "?";
                if (InPasteWait(vimBuffer))
                {
                    searchText += "\"";
                }
                return new EditableCommand(prefix + searchText);
            }

            switch (currentMode.ModeKind)
            {
                case ModeKind.Command:
                    {
                        var command = vimBuffer.CommandMode.EditableCommand;
                        var status = ":" + command.Text + (InPasteWait(vimBuffer) ? "\"" : "");
                        var caretPosition = command.CaretPosition + 1;
                        return new EditableCommand(status, caretPosition);
                    }
                case ModeKind.SubstituteConfirm:
                    {
                        var status = GetStatusSubstituteConfirm(vimBuffer.SubstituteConfirmMode);
                        return new EditableCommand(status);
                    }
                default:
                    break;
            }

            return GetStatusCommon(vimBuffer, currentMode);
        }

        private static EditableCommand GetStatusCommon(IVimBuffer vimBuffer, IMode currentMode)
        {
            // Calculate the argument string if we are in one time command mode
            string oneTimeArgument = null;
            if (vimBuffer.InOneTimeCommand.IsSome())
            {
                if (vimBuffer.InOneTimeCommand.Is(ModeKind.Insert))
                {
                    oneTimeArgument = "insert";
                }
                else if (vimBuffer.InOneTimeCommand.Is(ModeKind.Replace))
                {
                    oneTimeArgument = "replace";
                }
            }

            // Check if we can enable the command line to accept user input
            string status;
            switch (currentMode.ModeKind)
            {
                case ModeKind.Normal:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? string.Empty
                        : string.Format("-- ({0}) --", oneTimeArgument);
                    break;
                case ModeKind.Command:
                    {
                        var command = vimBuffer.CommandMode.EditableCommand;
                        return new EditableCommand(":" + command.Text, command.CaretPosition + 1);
                    }
                case ModeKind.Insert:
                    status = "-- INSERT --";
                    break;
                case ModeKind.Replace:
                    status = "-- REPLACE --";
                    break;
                case ModeKind.VisualBlock:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "-- VISUAL BLOCK --"
                        : string.Format("--({0}) VISUAL BLOCK --", oneTimeArgument);
                    break;
                case ModeKind.VisualCharacter:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "-- VISUAL --"
                        : string.Format("-- ({0}) VISUAL --", oneTimeArgument);
                    break;
                case ModeKind.VisualLine:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "--VISUAL LINE--"
                        : string.Format("--({0}) VISUAL LINE --", oneTimeArgument);
                    break;
                case ModeKind.SelectBlock:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "-- SELECT BLOCK --"
                        : string.Format("-- ({0}) SELECT BLOCK --", oneTimeArgument);
                    break;
                case ModeKind.SelectCharacter:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "--SELECT--"
                        : string.Format("--({0}) SELECT--", oneTimeArgument);
                    break;
                case ModeKind.SelectLine:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? "-- SELECT LINE --"
                        : string.Format("-- ({0}) SELECT LINE --", oneTimeArgument);
                    break;
                case ModeKind.ExternalEdit:
                    status = "External edit detected (hit &lt;Esc&gt; to return to previous mode, &lt;C-c&gt; to cancel external edit)";
                    break;
                case ModeKind.Disabled:
                    status = vimBuffer.DisabledMode.HelpMessage;
                    break;
                case ModeKind.SubstituteConfirm:
                    status = GetStatusSubstituteConfirm(vimBuffer.SubstituteConfirmMode);
                    break;
                default:
                    status = string.Empty;
                    break;
            }

            return new EditableCommand(status);
        }

        private static string GetStatusSubstituteConfirm(ISubstituteConfirmMode mode)
        {
            var replace = mode.CurrentSubstitute.SomeOrDefault("");
            return string.Format("replace with {0} (y/n/a/q/l/^E/^Y)?", replace);
        }
    }
}
