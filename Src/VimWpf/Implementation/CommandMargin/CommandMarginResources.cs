using System;
using System.Collections.Generic;
using System.Text;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    internal static class CommandMarginResources
    {
        internal const string ExternalEditBanner = "External edit detected (hit <Esc> to return to previous mode, <C-c> to cancel external edit)";
        internal const string InsertBanner = "-- INSERT --";
        internal const string ReplaceBanner = "-- REPLACE --";
        internal const string SelectBlockBanner = "-- SELECT BLOCK --";
        internal const string SelectBlockOneTimeCommandBanner = "-- ({0}) SELECT BLOCK --";
        internal const string SelectCharacterBanner = "-- SELECT --";
        internal const string SelectCharacterOneTimeCommandBanner = "-- ({0}) SELECT --";
        internal const string SelectLineBanner = "-- SELECT LINE --";
        internal const string SelectLineOneTimeCommandBanner = "-- ({0}) SELECT LINE --";
        internal const string SubstituteConfirmBanner = "replace with {0} (y/n/a/q/l/^E/^Y)?";
        internal const string VisualBlockBanner = "-- VISUAL BLOCK --";
        internal const string VisualBlockOneTimeCommandBanner = "-- ({0}) VISUAL BLOCK --";
        internal const string VisualCharacterBanner = "-- VISUAL --";
        internal const string VisualCharacterOneTimeCommandBanner = "-- ({0}) VISUAL --";
        internal const string VisualLineBanner = "-- VISUAL LINE --";
        internal const string VisualLineOneTimeCommandBanner = "-- ({0}) VISUAL LINE --";
    }
}
