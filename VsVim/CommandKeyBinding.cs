
namespace VsVim
{
    public sealed class CommandKeyBinding
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
            return Name + "::" + KeyBinding;
        }
    }
}
