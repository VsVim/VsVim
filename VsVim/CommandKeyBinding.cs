using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsVim
{
    public struct CommandKeyBinding
    {
        public readonly string Name;
        public readonly KeyBinding KeyBinding;

        public CommandKeyBinding(string name, KeyBinding binding)
        {
            Name = name;
            KeyBinding = binding;
        }

        public override string ToString()
        {
            return Name + "::" + KeyBinding.ToString();
        }
    }
}
