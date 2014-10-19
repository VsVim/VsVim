using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using Vim.Extensions;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    /// <summary>
    /// Factory for creating the margin for Vim
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Export(typeof(CommandMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType(VimConstants.ContentType)]
    [Name(CommandMargin.Name)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private static readonly object Key = new object();

        private readonly IVim _vim;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim, 
            IEditorFormatMapService editorFormatMapService,
            IClassificationFormatMapService classificationFormatMapService)
        {
            _vim = vim;
            _editorFormatMapService = editorFormatMapService;
            _classificationFormatMapService = classificationFormatMapService;
        }

        internal CommandMargin GetOrCreateCommandMargin(IVimBuffer vimBuffer)
        {
            CommandMargin commandMargin;
            if (!vimBuffer.Properties.TryGetPropertySafe(Key, out commandMargin))
            {
                commandMargin = CreateCommandMargin(vimBuffer);
                vimBuffer.Closed += delegate { vimBuffer.Properties.RemoveProperty(Key); };
            }

            return commandMargin;
        }

        private CommandMargin CreateCommandMargin(IVimBuffer vimBuffer)
        {
            Contract.Requires(!vimBuffer.TextView.IsClosed);

            var wpfTextView = (IWpfTextView)vimBuffer.TextView;
            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextView);
            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(wpfTextView);
            return new CommandMargin(wpfTextView.VisualElement, vimBuffer, editorFormatMap, classificationFormatMap);
        }

        #region IWpfTextViewMarginProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextViewHost.TextView, out vimBuffer))
            {
                return null;
            }

            return GetOrCreateCommandMargin(vimBuffer);
        }

        #endregion
    }
}
