using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VsVim.UI
{
    public sealed class KeyBindingData :DependencyObject
    {
        public static readonly DependencyProperty NameProperty = DependencyProperty.Register(
            "Name",
            typeof(string),
            typeof(KeyBindingData));

        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register(
            "Keys",
            typeof(string),
            typeof(KeyBindingData));

        public static readonly DependencyProperty CommandIdProperty = DependencyProperty.Register(
            "CommandId",
            typeof(Guid),
            typeof(KeyBindingData));

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            "IsChecked",
            typeof(bool),
            typeof(KeyBindingData));

        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        public string Keys
        {
            get { return (string)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public Guid CommandId
        {
            get { return (Guid)GetValue(CommandIdProperty); }
            set { SetValue(CommandIdProperty, value); }
        }

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
    }
}
