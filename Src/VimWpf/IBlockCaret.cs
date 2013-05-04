using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    public enum CaretDisplay
    {
        /// <summary>
        /// Full block caret used in normal mode
        /// </summary>
        Block,

        /// <summary>
        /// Half block used for operator pending
        /// </summary>
        HalfBlock,

        /// <summary>
        /// Quarter block commonly used in replace mode
        /// </summary>
        QuarterBlock,

        /// <summary>
        /// Invisible caret for items like incremental search
        /// </summary>
        Invisible,

        /// <summary>
        /// The normal caret for insert, disabled mode
        /// </summary>
        NormalCaret,

        /// <summary>
        /// Used for inclusize selections.  Different from NormalCaret because
        /// we want to hide the real caret.  Several extensions, namely VAX, key
        /// off of the real caret being hidden and let us have key strokes
        /// </summary>
        Select
    }

    public interface IBlockCaret
    {
        ITextView TextView { get; }
        CaretDisplay CaretDisplay { get; set; }
        double CaretOpacity { get; set; }
        void Destroy();
    }
}
