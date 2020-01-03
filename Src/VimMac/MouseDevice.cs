using System;
using System.ComponentModel.Composition;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.Mac
{
    [Export(typeof(IMouseDevice))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Mouse Device")]
    internal class MouseDevice : IMouseDevice
    {
        public bool IsLeftButtonPressed => false;

        public bool IsRightButtonPressed => false;

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool InDragOperation(ITextView textView)
        {
            return false;
        }
    }
}
