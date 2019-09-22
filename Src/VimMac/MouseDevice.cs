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
        public bool IsLeftButtonPressed => throw new NotImplementedException();

        public bool IsRightButtonPressed => throw new NotImplementedException();

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool InDragOperation(ITextView textView)
        {
            throw new NotImplementedException();
        }
    }
}
