using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Vim.VisualStudio.Implementation.OptionPages
{
    public sealed class KeyBindingData : DependencyObject
    {
        private readonly KeyBindingHandledByOption _visualStudioOption;
        private readonly KeyBindingHandledByOption _vsVimOption;
        private readonly ObservableCollection<KeyBindingHandledByOption> _handledByOptions = new ObservableCollection<KeyBindingHandledByOption>();
        private readonly ReadOnlyCollection<CommandKeyBinding> _bindings;

        public static readonly DependencyProperty KeyNameProperty = DependencyProperty.Register(
            "KeyName",
            typeof(string),
            typeof(KeyBindingData));

        public static readonly DependencyProperty SelectedHandledByOptionProperty = DependencyProperty.Register(
            "SelectedHandledByOption",
            typeof(KeyBindingHandledByOption),
            typeof(KeyBindingData));

        public string KeyName
        {
            get { return (string)GetValue(KeyNameProperty); }
            set { SetValue(KeyNameProperty, value); }
        }

        public bool HandledByVsVim
        {
            get { return SelectedHandledByOption == _vsVimOption; }
            set { SelectedHandledByOption = value ? _vsVimOption : _visualStudioOption; }
        }

        public KeyBindingHandledByOption SelectedHandledByOption
        {
            get { return (KeyBindingHandledByOption)GetValue(SelectedHandledByOptionProperty); }
            set { SetValue(SelectedHandledByOptionProperty, value); }
        }

        public ObservableCollection<KeyBindingHandledByOption> HandledByOptions
        {
            get { return _handledByOptions; }
        }

        public ReadOnlyCollection<CommandKeyBinding> Bindings
        {
            get { return _bindings; }
        }

        public KeyBindingData()
        {

        }

        public KeyBindingData(ReadOnlyCollection<CommandKeyBinding> bindings)
        {
            // All bindings passed have the same KeyInput as their first key, so get it
            var firstKeyInput = bindings.First().KeyBinding.FirstKeyStroke;
            KeyName = KeyBinding.CreateKeyBindingStringForSingleKeyStroke(firstKeyInput);

            // It's possible that Visual Studio will bind multiple key strokes to the same 
            // command.  Often it will be things like "Ctrl-[, P" and "Ctr-[, Ctrl-P".  In 
            // that case we don't want to list the command twice so filter that possibility
            // out here
            var commandNames = bindings.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase);

            _bindings = bindings;
            _handledByOptions.AddRange(
                new[] {
                     _visualStudioOption = new KeyBindingHandledByOption("Visual Studio", commandNames),
                     _vsVimOption = new KeyBindingHandledByOption("VsVim", Enumerable.Empty<string>())
                });
        }

        public override string ToString()
        {
            return string.Format("{0} - Handled by {1}",
                KeyName,
                HandledByVsVim ? "VsVim" : "Visual Studio");
        }
    }
}
