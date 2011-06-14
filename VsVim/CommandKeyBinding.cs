
namespace VsVim
{
    /// <summary>
    /// Represents the KeyBinding information for a Visual Studio command
    /// </summary>
    public sealed class CommandKeyBinding
    {
        /// <summary>
        /// Name of the Visual Studio Command
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// KeyBinding for this command
        /// </summary>
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
