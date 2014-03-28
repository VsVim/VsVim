using System.Windows;
using System.Windows.Controls;

namespace VsVim.Implementation.OptionPages
{
    /// <summary>
    /// Selects the appropriate expanded/collapsed template when showing a KeyBindingOption
    /// See http://social.msdn.microsoft.com/Forums/en-US/wpf/thread/cdd666d8-5d3d-4977-96ff-97305eaca644 for how this
    /// is used.
    /// </summary>
    internal sealed class ComboBoxTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var presenter = (ContentPresenter)container;

            if (presenter.TemplatedParent is ComboBox)
            {
                return (DataTemplate)presenter.FindResource("ComboBoxItemCollapsedTemplate");
            }
            else
            {
                return (DataTemplate)presenter.FindResource("ComboBoxItemExpandedTemplate");
            }
        }
    }
}
