using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace VsVim.UI
{
    public sealed class KeyBindingData : DependencyObject
    {
        private readonly KeyBindingHandledByOption _visualStudioOption;
        private readonly KeyBindingHandledByOption _vsVimOption;
        private readonly ObservableCollection<KeyBindingHandledByOption> _handledByOptions = new ObservableCollection<KeyBindingHandledByOption>();

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

        public IEnumerable<CommandKeyBinding> Bindings { get; private set; }

        public KeyBindingData()
        {

        }

        public KeyBindingData(IEnumerable<CommandKeyBinding> bindings)
        {
            // All bindings passed have the same KeyInput as their first key, so get it
            var firstKeyInput = bindings.First().KeyBinding.FirstKeyStroke;
            KeyName = KeyBinding.CreateKeyBindingStringForSingleKeyStroke(firstKeyInput);

            Bindings = bindings.ToArray();
            _handledByOptions.AddRange(
                new[] {
                     _visualStudioOption = new KeyBindingHandledByOption("Visual Studio", bindings.Select(binding => binding.Name)),
                     _vsVimOption = new KeyBindingHandledByOption("VsVim", Enumerable.Empty<string>())
                });
        }
    }
}
