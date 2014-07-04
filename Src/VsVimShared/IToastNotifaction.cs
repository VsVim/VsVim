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

        void Display(FrameworkElement frameworkElement);
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
