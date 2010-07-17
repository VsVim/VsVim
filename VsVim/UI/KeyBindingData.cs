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

        public static readonly DependencyProperty HandledByVsVimProperty = DependencyProperty.Register(
            "HandledByVsVim",
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

        public bool HandledByVsVim
        {
            get { return (bool)GetValue(HandledByVsVimProperty); }
            set { SetValue(HandledByVsVimProperty, value); }
        }

        private CommandKeyBinding[] _bindings;
        private List<KeyBindingHandledByOption> _handledByOptions;

        public KeyBindingData()
        {
        }

        public IEnumerable<CommandKeyBinding> Bindings
        {
            get { return _bindings; }
        }

        public List<KeyBindingHandledByOption> HandledByOptions
        {
            get { return _handledByOptions; }
        }

        public KeyBindingData(IEnumerable<CommandKeyBinding> bindings)
        {
            // All bindings passed have the same KeyInput as their first key, so get it
            Vim.KeyInput firstKeyInput = bindings.First().KeyBinding.FirstKeyInput;
            KeyName = KeyBinding.CreateKeyBindingStringForSingleKeyInput(firstKeyInput);

            _bindings = bindings.ToArray();
            _handledByOptions = new List<KeyBindingHandledByOption>()
                                {
                                    new KeyBindingHandledByOption("Visual Studio", bindings.Select(binding => binding.Name)),
                                    new KeyBindingHandledByOption("VsVim", Enumerable.Empty<string>())
                                };
        }
    }
}
