
using System;
namespace VsVim
{
    /// <summary>
    /// Id of an EnvDTE.Command object.  The Name portion of a Command is optional and doesn't
    /// figure into the name.  The Guid and ID are unique to a command
    /// </summary>
    public struct CommandId
    {
        public readonly Guid Group;

        public readonly uint Id;

        public CommandId(Guid group, uint id)
        {
            Group = group;
            Id = id;
        }
    }

    /// <summary>
    /// Represents the KeyBinding information for a Visual Studio command
    /// </summary>
    public sealed class CommandKeyBinding
    {
        /// <summary>
        /// The unique id of the command
        /// </summary>
        public readonly CommandId Id;

        /// <summary>
        /// Name of the Visual Studio Command
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// KeyBinding for this command
        /// </summary>
        public readonly KeyBinding KeyBinding;

        public CommandKeyBinding(CommandId commandId, string name, KeyBinding binding)
        {
            Id = commandId;
            Name = name;
            KeyBinding = binding;
        }

        public override string ToString()
        {
            return Name + "::" + KeyBinding;
        }
    }
}
