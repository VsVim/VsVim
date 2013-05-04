using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim
{
    /// <summary>
    /// This represents an abstraction over the Report Expression Designer.  This is a custom hosting
    /// of the Visual Studio editor which does some odd keyboard processing that we have to special case
    /// in VsVim.  
    /// 
    /// An instance of the designer can be created by doing the following 
    ///     - Add New Item
    ///     - Reporting -> Report
    ///     - Add a Table
    ///     - Right click and select "Expression"
    ///     
    /// </summary>
    internal interface IReportDesignerUtil
    {
        /// <summary>
        /// Is this ITextView hosted in the report designer
        /// </summary>
        bool IsExpressionView(ITextView textView);

        /// <summary>
        /// Is this one of the KeyInput values that the expression view specially handles
        /// </summary>
        bool IsSpecialHandled(KeyInput keyInput);
    }
}
