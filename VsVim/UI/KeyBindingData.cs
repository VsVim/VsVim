using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsVim.UI
{
    public sealed class KeyBindingData
    {
        public string Name { get; set; }
        public string Keys { get; set; }
        public Guid CommandId { get; set; }
        public bool IsChecked { get; set; }
    }
}
