﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Vim;
using Vim.VisualStudio;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.OptionPages
{
    /// <summary>
    /// Interaction logic for KeyboardSettingsControl.xaml
    /// </summary>
    public partial class KeyboardSettingsControl : UserControl
    {
        public static readonly DependencyProperty IncludeAllScopesProperty = DependencyProperty.Register(
            "IncludeAllScopes",
            typeof(bool),
            typeof(KeyboardSettingsControl));

        private readonly ObservableCollection<KeyBindingData> _keyBindingList = new ObservableCollection<KeyBindingData>();
        private readonly HashSet<KeyBindingData> _advancedSet = new HashSet<KeyBindingData>();
        private readonly IVim _vim;
        private readonly IKeyBindingService _keyBindingService;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IProtectedOperations _protectedOperations;
        private CommandKeyBindingSnapshot _snapshot;

        public bool IncludeAllScopes
        {
            get { return (bool)GetValue(IncludeAllScopesProperty); }
            set { SetValue(IncludeAllScopesProperty, value); }
        }

        public ObservableCollection<KeyBindingData> KeyBindingList
        {
            get { return _keyBindingList; }
        }

        public KeyboardSettingsControl(IVim vim, IKeyBindingService keyBindingService, IVimApplicationSettings vimApplicationSettings, IProtectedOperations protectedOperations)
        {
            InitializeComponent();

            _vim = vim;
            _keyBindingService = keyBindingService;
            _vimApplicationSettings = vimApplicationSettings;
            _protectedOperations = protectedOperations;
            IncludeAllScopes = keyBindingService.IncludeAllScopes;
            BindingsListBox.Items.SortDescriptions.Add(new SortDescription("KeyName", ListSortDirection.Ascending));
            ComputeKeyBindings();
        }

        public void Apply()
        {
            try
            {
                UpdateKeyBindings();
            }
            catch (Exception ex)
            {
                // This code is run on the core message loop.  An exception here is fatal and there
                // are a good number of COM calls here.  Catch the exception and report the error to
                // the user 
                _protectedOperations.Report(ex);
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IncludeAllScopesProperty && IncludeAllScopes != _keyBindingService.IncludeAllScopes)
            {
                _keyBindingService.IncludeAllScopes = IncludeAllScopes;
                ComputeKeyBindings();
            }
        }

        private void ComputeKeyBindings()
        {
            // This snapshot contains a list of active keys, and keys which are still conflicting. We will group all
            // bindings by the initial character, and will consider the entire group as being handled by VsVim as long
            // as one is being handled.
            _snapshot = GetCommandKeyBindingSnapshot();
            _keyBindingList.Clear();
            _advancedSet.Clear();

            var handledByVsVim = _snapshot.Removed.ToLookup(binding => binding.KeyBinding.FirstKeyStroke);
            var handledByVs = _snapshot.Conflicting.ToLookup(binding => binding.KeyBinding.FirstKeyStroke);

            var allFirstKeys = handledByVsVim.Select(group => group.Key)
                               .Union(handledByVs.Select(group => group.Key));

            foreach (var firstKey in allFirstKeys)
            {
                var data = new KeyBindingData(handledByVsVim[firstKey].Union(handledByVs[firstKey]).ToReadOnlyCollection())
                {
                    HandledByVsVim = handledByVsVim.Contains(firstKey)
                };
                _keyBindingList.Add(data);

                if (IsAdvanced(firstKey))
                {
                    _advancedSet.Add(data);
                }
            }
        }

        /// <summary>
        /// Switch all of the bindings to VsVim except for the advanced ones
        /// </summary>
        private void OnEnableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList
                .Where(x => !_advancedSet.Contains(x))
                .ForEach(x => x.HandledByVsVim = true);
        }

        /// <summary>
        /// Switch all of the bindings to Visual Studio
        /// </summary>
        private void OnDisableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList.ForEach(x => x.HandledByVsVim = false);
        }

        /// <summary>
        /// Is this an advanced key stroke like CTRL+V which we don't want to automatically 
        /// convert.  
        /// </summary>
        private bool IsAdvanced(KeyStroke keyStroke)
        {
            // Look for paste
            if (keyStroke.KeyModifiers != VimKeyModifiers.Control || keyStroke.KeyInput.KeyModifiers != VimKeyModifiers.None)
            {
                return false;
            }

            // Look for paste, cut, copy and select all
            switch (keyStroke.KeyInput.Char)
            {
                case 'v':
                case 'x':
                case 'c':
                case 'a':
                    return true;
                default:
                    return false;
            }
        }

        private void UpdateKeyBindings()
        {
            // This process can both add and remove bindings to Visual Studio commands.  We need to look at 
            // each command individually and build up the correct key binding for that particular command
            // based on the current state of the model 
            var commandIdList = _keyBindingList
                .SelectMany(x => x.Bindings)
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            // Create a HashSet<string> of all key strokes that we've determined to be handled by 
            // VsVim through this UI.  
            var comparer = StringComparer.OrdinalIgnoreCase;
            var vsVimSet = new HashSet<string>(
                _keyBindingList.Where(x => x.HandledByVsVim).SelectMany(x => x.Bindings).Select(x => x.KeyBinding.CommandString).Distinct(comparer),
                comparer);

            foreach (var commandId in commandIdList)
            {
                if (!_snapshot.TryGetCommandData(commandId, out EnvDTE.Command command, out ReadOnlyCollection<CommandKeyBinding> currentBindingList))
                {
                    continue;
                }

                // Next get the KeyBinding values which are required by the model itself.  This will be all 
                // of the KeyBinding values the UI decided needed to be handled by Visual Studio
                var vsBindingList = _keyBindingList
                    .Where(x => !x.HandledByVsVim)
                    .SelectMany(x => x.Bindings)
                    .Where(x => x.Id == commandId)
                    .Select(x => x.KeyBinding.CommandString)
                    .ToList();

                // Now we need to look at the current bindings and find the ones that are completely unrelated
                // to this process that need to be kept.  For example VsVim would never care about handling 
                // Ctrl+Shift+F5 but it's possible the user bound it to Edit.BreakLine.  We don't want to remove
                // that binding as a part of transferring say Ctrl-[ to VsVim.
                foreach (var currentBinding in currentBindingList)
                {
                    var commandString = currentBinding.KeyBinding.CommandString;
                    if (!vsVimSet.Contains(commandString))
                    {
                        vsBindingList.Add(commandString);
                    }
                }

                command.SafeSetBindings(vsBindingList);
            }

            _vimApplicationSettings.RemovedBindings =
                _keyBindingList.Where(binding => binding.HandledByVsVim).SelectMany(data => data.Bindings)
                .ToReadOnlyCollection();
            _vimApplicationSettings.HaveUpdatedKeyBindings = true;
        }

        private CommandKeyBindingSnapshot GetCommandKeyBindingSnapshot()
        {
            var textView = _vim.VimHost.CreateHiddenTextView();
            try
            {
                var vimBuffer = _vim.CreateVimBuffer(textView);
                return _keyBindingService.CreateCommandKeyBindingSnapshot(vimBuffer);
            }
            finally
            {
                textView.Close();
            }
        }
    }
}
