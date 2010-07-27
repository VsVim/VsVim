using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;

namespace VsVim.UI
{
    /// <summary>Selects the appropriate expanded/collapsed template when showing a KeyBindingOption.</summary>
    /// <remarks>
    /// See http://social.msdn.microsoft.com/Forums/en-US/wpf/thread/cdd666d8-5d3d-4977-96ff-97305eaca644 for how this
    /// is used.
    /// </remarks>
    internal class ComboBoxTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            ContentPresenter presenter = (ContentPresenter)container;

            if (presenter.TemplatedParent is ComboBox)
                return (DataTemplate)presenter.FindResource("ComboBoxItemCollapsedTemplate");
            else
                return (DataTemplate)presenter.FindResource("ComboBoxItemExpandedTemplate");
        }
    }
}
