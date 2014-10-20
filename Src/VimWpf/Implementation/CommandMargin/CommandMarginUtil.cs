using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    [Export(typeof(ICommandMarginUtil))]
    internal sealed class CommandMarginUtil : ICommandMarginUtil
    {
        CommandMarginProvider _provider;

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
            CommandMargin commandMargin;
            if (_provider.TryGetCommandMargin(vimBuffer, out commandMargin))
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
            if (search.InSearch && search.InPasteWait)
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
            if (search.InSearch)
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
                case ModeKind.Normal:
                    status = vimBuffer.NormalMode.Command;
                    break;
                case ModeKind.SubstituteConfirm:
                    status = GetStatusSubstituteConfirm(vimBuffer.SubstituteConfirmMode);
                    break;
                case ModeKind.VisualBlock:
                    status = GetStatusWithRegister(Resources.VisualBlockBanner, vimBuffer.VisualBlockMode.CommandRunner);
                    break;
                case ModeKind.VisualCharacter:
                    status = GetStatusWithRegister(Resources.VisualCharacterBanner, vimBuffer.VisualCharacterMode.CommandRunner);
                    break;
                case ModeKind.VisualLine:
                    status = GetStatusWithRegister(Resources.VisualLineBanner, vimBuffer.VisualLineMode.CommandRunner);
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
            var search = vimBuffer.IncrementalSearch;
            string status;
            switch (currentMode.ModeKind)
            {
                case ModeKind.Normal:
                    status = String.IsNullOrEmpty(oneTimeArgument)
                        ? String.Empty
                        : String.Format(Resources.NormalOneTimeCommandBanner, oneTimeArgument);
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
                    status = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualBlockBanner
                        : String.Format(Resources.VisualBlockOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualCharacter:
                    status = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualCharacterBanner
                        : String.Format(Resources.VisualCharacterOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualLine:
                    status = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualLineBanner
                        : String.Format(Resources.VisualLineOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.SelectBlock:
                    status = Resources.SelectBlockBanner;
                    break;
                case ModeKind.SelectCharacter:
                    status = Resources.SelectCharacterBanner;
                    break;
                case ModeKind.SelectLine:
                    status = Resources.SelectLineBanner;
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
                    status = String.Empty;
                    break;
            }

            return status;
        }

        private static string GetStatusSubstituteConfirm(ISubstituteConfirmMode mode)
        {
            var replace = mode.CurrentSubstitute.SomeOrDefault("");
            return String.Format(Resources.SubstituteConfirmBannerFormat, replace);
        }

        private static string GetStatusWithRegister(string commandLine, ICommandRunner commandRunner)
        {
            if (commandRunner.HasRegisterName && commandRunner.RegisterName.Char.IsSome())
            {
                commandLine = string.Format("{0} \"{1}", commandLine, commandRunner.RegisterName.Char.Value);
            }

            return commandLine;
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
    }
}
