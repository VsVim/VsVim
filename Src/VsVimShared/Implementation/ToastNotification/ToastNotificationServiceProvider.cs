using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.VisualStudio.Implementation.ToastNotification
{
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType("text")]
    [Name(ToastNotificationService.MarginName)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Export(typeof(IToastNotificationServiceProvider))]
    internal sealed class ToastNotificationServiceProvider : IToastNotificationServiceProvider, IWpfTextViewMarginProvider
    {
        internal static readonly object Key = new object();

        private readonly IEditorFormatMapService _editorFormatMapService;

        [ImportingConstructor]
        internal ToastNotificationServiceProvider(IEditorFormatMapService editorFormatMapService)
        {
            _editorFormatMapService = editorFormatMapService;
        }

        private ToastNotificationService GetOrCreate(IWpfTextView wpfTextView)
        {
            return wpfTextView.Properties.GetOrCreateSingletonProperty(Key, () =>
            {
                var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextView);
                return new ToastNotificationService(wpfTextView, editorFormatMap);
            });
        }

        #region IToastNotificationServiceProvider

        IToastNotificationService IToastNotificationServiceProvider.GetToastNoficationService(IWpfTextView wpfTextView)
        {
            return GetOrCreate(wpfTextView);
        }

        #endregion

        #region IWpfTextViewMarginProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            return GetOrCreate(wpfTextViewHost.TextView);
        }

        #endregion
    }
}
