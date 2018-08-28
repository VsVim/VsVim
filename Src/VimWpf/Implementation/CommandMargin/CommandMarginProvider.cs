using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using Vim.Extensions;

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
    [Order(After = "Wpf Horizontal Scrollbar")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private static readonly object s_key = new object();

        private readonly IVim _vim;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly ICommonOperationsFactory _commonOperationsFactory;
        private readonly IClipboardDevice _clipboardDevice;

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim,
            IEditorFormatMapService editorFormatMapService,
            IClassificationFormatMapService classificationFormatMapService,
            ICommonOperationsFactory commonOperationsFactory,
            IClipboardDevice clipboardDevice)
        {
            _vim = vim;
            _editorFormatMapService = editorFormatMapService;
            _classificationFormatMapService = classificationFormatMapService;
            _commonOperationsFactory = commonOperationsFactory;
            _clipboardDevice = clipboardDevice;
        }

        internal bool TryGetCommandMargin(IVimBuffer vimBuffer, out CommandMargin commandMargin)
        {
            return vimBuffer.Properties.TryGetPropertySafe(s_key, out commandMargin);
        }

        private CommandMargin CreateCommandMargin(IVimBuffer vimBuffer)
        {
            Contract.Requires(!vimBuffer.TextView.IsClosed);

            var wpfTextView = (IWpfTextView)vimBuffer.TextView;
            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextView);
            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(wpfTextView);
            var commonOperations = _commonOperationsFactory.GetCommonOperations(vimBuffer.VimBufferData);
            var commandMargin = new CommandMargin(wpfTextView.VisualElement, vimBuffer, editorFormatMap, classificationFormatMap, commonOperations, _clipboardDevice);

            vimBuffer.Properties.AddProperty(s_key, commandMargin);
            vimBuffer.Closed += delegate { vimBuffer.Properties.RemoveProperty(s_key); };

            return commandMargin;
        }

        #region IWpfTextViewMarginProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextViewHost.TextView, out IVimBuffer vimBuffer))
            {
                return null;
            }

            return CreateCommandMargin(vimBuffer);
        }

        #endregion
    }
}
