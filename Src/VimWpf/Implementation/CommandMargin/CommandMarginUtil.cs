using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    [Export(typeof(ICommandMarginUtil))]
    internal sealed class CommandMarginUtil : ICommandMarginUtil
    {
        private CommandMarginProvider _provider;

        [ImportingConstructor]
        internal CommandMarginUtil(CommandMarginProvider provider)
        {
            _provider = provider;
        }

        private void SetMarginVisibility(IVimBuffer vimBuffer, bool commandMarginVisible)
        {
            if (vimBuffer.TextView.IsClosed)
            {
                return;
            }

            // It's important that we not force create the CommandMargin here.  It is a margin and depends on 
            // the margin implementation being disposed to avoid memory leaks.  The editor framework though will
            // only call Dispose if it attempted to create the margin.  If we create the margin and the editor does 
            // not then Dispose won't be called and a memory leak will occur 
            if (_provider.TryGetCommandMargin(vimBuffer, out CommandMargin commandMargin))
            {
                commandMargin.Enabled = commandMarginVisible;
            }
        }

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

        internal static string GetStatus(IVimBuffer vimBuffer, IMode currentMode, bool forModeSwitch)
        {
            if (forModeSwitch)
            {
                return GetStatusCommon(vimBuffer, currentMode);
            }

            return GetStatusOther(vimBuffer, currentMode);
        }

        private static string GetStatusOther(IVimBuffer vimBuffer, IMode currentMode)
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
                return prefix + searchText;
            }

            string status;
            switch (currentMode.ModeKind)
            {
                case ModeKind.Command:
                    status = ":" + vimBuffer.CommandMode.Command + (InPasteWait(vimBuffer) ? "\"" : "");
                    break;
                case ModeKind.SubstituteConfirm:
                    status = GetStatusSubstituteConfirm(vimBuffer.SubstituteConfirmMode);
                    break;
                default:
                    status = GetStatusCommon(vimBuffer, currentMode);
                    break;
            }

            return status;
        }

        private static string GetStatusCommon(IVimBuffer vimBuffer, IMode currentMode)
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
                        : string.Format(Resources.NormalOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.Command:
                    status = ":" + vimBuffer.CommandMode.Command;
                    break;
                case ModeKind.Insert:
                    status = Resources.InsertBanner;
                    break;
                case ModeKind.Replace:
                    status = Resources.ReplaceBanner;
                    break;
                case ModeKind.VisualBlock:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualBlockBanner
                        : string.Format(Resources.VisualBlockOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualCharacter:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualCharacterBanner
                        : string.Format(Resources.VisualCharacterOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualLine:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualLineBanner
                        : string.Format(Resources.VisualLineOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.SelectBlock:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.SelectBlockBanner
                        : string.Format(Resources.SelectBlockOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.SelectCharacter:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.SelectCharacterBanner
                        : string.Format(Resources.SelectCharacterOneTimeCommandBanner, oneTimeArgument); 
                    break;
                case ModeKind.SelectLine:
                    status = string.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.SelectLineBanner
                        : string.Format(Resources.SelectLineOneTimeCommandBanner, oneTimeArgument); 
                    break;
                case ModeKind.ExternalEdit:
                    status = Resources.ExternalEditBanner;
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

            return status;
        }

        private static string GetStatusSubstituteConfirm(ISubstituteConfirmMode mode)
        {
            var replace = mode.CurrentSubstitute.SomeOrDefault("");
            return string.Format(Resources.SubstituteConfirmBannerFormat, replace);
        }

        #region ICommandMarginUtil

        void ICommandMarginUtil.SetMarginVisibility(IVimBuffer vimBuffer, bool commandMarginVisible)
        {
            SetMarginVisibility(vimBuffer, commandMarginVisible);
        }

        string ICommandMarginUtil.GetStatus(IVimBuffer vimBuffer)
        {
            return GetStatus(vimBuffer, vimBuffer.Mode, forModeSwitch: false);
        }

        #endregion

        public static string GetShowCommandText(IVimBuffer vimBuffer)
        {
            if (vimBuffer.IncrementalSearch.HasActiveSession)
            {
                return string.Empty;
            }

            switch (vimBuffer.ModeKind)
            {
                case ModeKind.Normal:
                    return GetNormalModeShowCommandText(vimBuffer);
                case ModeKind.SelectBlock:
                    return GetVisualModeShowCommandText(vimBuffer, vimBuffer.VisualBlockMode);
                case ModeKind.SelectCharacter:
                    return GetVisualModeShowCommandText(vimBuffer, vimBuffer.VisualCharacterMode);
                case ModeKind.SelectLine:
                    return GetVisualModeShowCommandText(vimBuffer, vimBuffer.VisualLineMode);
                case ModeKind.VisualCharacter:
                case ModeKind.VisualBlock:
                case ModeKind.VisualLine:
                    return GetVisualModeShowCommandText(vimBuffer, (IVisualMode) vimBuffer.Mode);
            }

            return string.Empty;
        }

        private static string GetNormalModeShowCommandText(IVimBuffer vimBuffer)
        {
            var normalMode = vimBuffer.NormalMode;
            var cmdText = KeyInputsToShowCommandText(normalMode.CommandRunner.Inputs.Concat(vimBuffer.BufferedKeyInputs));
            return string.IsNullOrEmpty(cmdText) ? normalMode.Command : cmdText;
        }

        private static string GetVisualModeShowCommandText(IVimBuffer vimBuffer, IVisualMode visualMode)
        {
            var cmdText = KeyInputsToShowCommandText(visualMode.CommandRunner.Inputs.Concat(vimBuffer.BufferedKeyInputs));
            if (!string.IsNullOrEmpty(cmdText))
            {
                return cmdText;
            }

            var visualSpan = visualMode.VisualSelection.VisualSpan;
            if (!visualSpan.Spans.Any())
            {
                return string.Empty; // not sure if this can happen
            }

            switch (visualSpan.VisualKind.VisualModeKind)
            {
                case ModeKind.VisualLine:
                    return visualSpan.LineRange.Count.ToString();
                case ModeKind.VisualCharacter:
                    if (visualSpan.LineRange.Count > 1)
                    {
                        return visualSpan.LineRange.Count.ToString();
                    }
        
                    var charSpan = visualSpan.Spans.First();
                    // account for the selection possibly extending past the last printable character in the line to include some or all of a multi-character newline
                    var line = charSpan.Snapshot.GetLineFromPosition(charSpan.Start);
                    if (charSpan.End.Position > line.End)
                    {
                        return (line.End - charSpan.Start + 1).ToString();
                    }

                    return charSpan.Length.ToString();
                case ModeKind.VisualBlock:
                    return $"{visualSpan.LineRange.Count}x{visualSpan.Spans.Max(x => x.Length)}";
            }

            return string.Empty;
        }

        private static string KeyInputsToShowCommandText(IEnumerable<KeyInput> inputs)
        {
            return string.Concat(inputs.Select(x =>
                                               {
                                                   string text;
                                                   return CharDisplay.ControlCharUtil.TryGetDisplayText(x.Char, out text) ? text : x.Char.ToString();
                                               }));
        }
    }
}
