using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    [Export(typeof(ICommandMarginUtil))]
    internal sealed class CommandMarginUtil : ICommandMarginUtil
    {

        private void SetMarginVisibility(IVimBuffer vimBuffer, bool commandMarginVisible)
        {
            // TODO: implement
        }

        internal static string GetStatus(IVimBuffer vimBuffer, IMode currentMode)
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
                    status = GetStatus(vimBuffer.SubstituteConfirmMode);
                    break;
                default:
                    status = String.Empty;
                    break;
            }

            return status;
        }

        internal static string GetStatus(ISubstituteConfirmMode mode)
        {
            var replace = mode.CurrentSubstitute.SomeOrDefault("");
            return String.Format(Resources.SubstituteConfirmBannerFormat, replace);
        }

        #region ICommandMarginUtil

        void ICommandMarginUtil.SetMarginVisibility(IVimBuffer vimBuffer, bool commandMarginVisible)
        {
            SetMarginVisibility(vimBuffer, commandMarginVisible);
        }

        string ICommandMarginUtil.GetStatus(IVimBuffer vimBuffer)
        {
            return GetStatus(vimBuffer, vimBuffer.Mode);
        }

        #endregion
    }
}
