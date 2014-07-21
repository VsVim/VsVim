using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Vim;

namespace VimApp.Implementation.NewLineDisplay
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class NewLineDisplayFactory : IWpfTextViewCreationListener
    {
        internal const string AdornmentLayerName = "NewLineDisplayAdornment";

        private readonly IVimAppOptions _vimAppOptions;

        /// <summary>
        /// Defines the adornment layer for the adornment. This layer is ordered 
        /// after the selection layer in the Z-order
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(AdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition NewLineDisplayAdornmentLayerDefinition = null;

        [ImportingConstructor]
        internal NewLineDisplayFactory(IVimAppOptions vimAppOptions)
        {
            _vimAppOptions = vimAppOptions;
        }

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            new NewLineDisplay(textView, textView.GetAdornmentLayer(AdornmentLayerName), _vimAppOptions);
        }
    }
}
