using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    public interface IToastNotificationService
    {
        IWpfTextView TextView { get; } 

        /// <summary>
        /// Display the given toast notification.  Optionally also provide a callback to be 
        /// invoked whenever the notifaction is removed
        /// </summary>
        void Display(FrameworkElement toastNotifaction, Action onRemoveCallback = null);

        /// <summary>
        /// Remove the toast notification from the display.  Returns true if the value was
        /// actually removed
        /// </summary>
        bool Remove(FrameworkElement toastNotifaction);
    }

    /// <summary>
    /// This is a MEF importable interface which provides instances of IToastNotificationService
    /// for a given IWpfTextView
    /// </summary>
    public interface IToastNotificationServiceProvider
    {
        IToastNotificationService GetToastNoficationService(IWpfTextView wpfTextView);
    }
}
