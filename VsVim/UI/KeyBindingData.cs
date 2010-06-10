using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VsVim.UI
{
    public sealed class KeyBindingData : DependencyObject
    {

        public static readonly DependencyProperty KeyNameProperty = DependencyProperty.Register(
            "KeyName",
            typeof(string),
            typeof(KeyBindingData));

        public static readonly DependencyProperty DisabledCommandsProperty = DependencyProperty.Register(
            "DisabledCommands",
            typeof(string),
            typeof(KeyBindingData));

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            "IsChecked",
            typeof(bool),
            typeof(KeyBindingData));

        public string KeyName
        {
            get { return (string)GetValue(KeyNameProperty); }
            set { SetValue(KeyNameProperty, value); }
        }

        public string DisabledCommands
        {
            get { return (string)GetValue(DisabledCommandsProperty); }
            set { SetValue(DisabledCommandsProperty, value); }
        }

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        private CommandKeyBinding[] _bindings;

        public KeyBindingData()
        {

        }

        public IEnumerable<CommandKeyBinding> Bindings
        {
            get { return _bindings; }
        }

        public KeyBindingData(IEnumerable<CommandKeyBinding> bindings)
        {
            // All bindings passed have the same KeyInput as their first key, so get it
            Vim.KeyInput firstKeyInput = bindings.First().KeyBinding.FirstKeyInput;
            KeyName = KeyBinding.CreateKeyBindingStringForSingleKeyInput(firstKeyInput);

            _bindings = bindings.ToArray();

            DisabledCommands = "Interferes with " + string.Join(", ", bindings.Select(binding => binding.Name));
        }
    }
}
