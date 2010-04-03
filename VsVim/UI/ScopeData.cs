using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace VsVim.UI
{
    public class ScopeData
    {
        private ObservableCollection<KeyBindingData> _col = new ObservableCollection<KeyBindingData>();
        public string Name { get; set; }
        public ObservableCollection<KeyBindingData> KeyBindings { get { return _col; } }
    }
}
