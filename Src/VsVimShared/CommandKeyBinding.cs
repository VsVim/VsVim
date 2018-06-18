using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Id of an EnvDTE.Command object.  The Name portion of a Command is optional and doesn't
    /// figure into the name.  The Guid and ID are unique to a command
    /// </summary>
    public readonly struct CommandId : IEquatable<CommandId>
    {
        public readonly Guid Group;

        public readonly uint Id;

        public CommandId(Guid group, uint id)
        {
            Group = group;
            Id = id;
        }

        public bool Equals(CommandId other)
        {
            return Id == other.Id && Group == other.Group;
        }

        public override bool Equals(object obj)
        {
            if (obj is CommandId)
            {
                return Equals((CommandId)obj);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (int)Id ^ Group.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Group} - {Id}";
        }

        public static bool operator ==(CommandId left, CommandId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandId left, CommandId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Represents the KeyBinding information for a Visual Studio command
    /// </summary>
    public sealed class CommandKeyBinding : IEquatable<CommandKeyBinding>
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

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ KeyBinding.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CommandKeyBinding);
        }

        public bool Equals(CommandKeyBinding other)
        {
            if (other == null)
            {
                return false;
            }

            return
                Id == other.Id &&
                StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name) &&
                KeyBinding == other.KeyBinding;
        }

        public static bool operator ==(CommandKeyBinding left, CommandKeyBinding right)
        {
            return EqualityComparer<CommandKeyBinding>.Default.Equals(left, right);
        }

        public static bool operator !=(CommandKeyBinding left, CommandKeyBinding right)
        {
            return !EqualityComparer<CommandKeyBinding>.Default.Equals(left, right);
        }

        public override string ToString()
        {
            return Name + "::" + KeyBinding;
        }
    }
}
