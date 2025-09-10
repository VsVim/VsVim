using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.IntelliCode;

[Export(typeof(IExtensionAdapter))]
internal sealed class IntelliCodeExtensionAdapter : VimExtensionAdapter
{
    private const string DifferenceViewerWithAdaptersRole = nameof(DifferenceViewerWithAdaptersRole);

    [ImportingConstructor]
    internal IntelliCodeExtensionAdapter()
    {
    }

    // Suppress VsVim in the Embedded Difference Control elements.
    protected override bool ShouldCreateVimBuffer(ITextView textView) =>
        !IsEmbeddedDifferenceControlWindow(textView);

    private static bool IsEmbeddedDifferenceControlWindow(ITextView textView)
    {
        if (textView.Roles.Contains(DifferenceViewerWithAdaptersRole))
        {
            return true;
        }

        return false;
    }
}
