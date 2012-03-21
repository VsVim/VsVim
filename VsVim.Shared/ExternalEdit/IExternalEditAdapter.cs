using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.ExternalEdit
{
    internal interface IExternalEditAdapter
    {
        /// <summary>
        /// Does this IVsTextLineMarker represent an external edit 
        /// </summary>
        bool IsExternalEditMarker(IVsTextLineMarker marker);

        /// <summary>
        /// Does this ITag represent an external edit
        /// </summary>
        bool IsExternalEditTag(ITag tag);
    }
}
