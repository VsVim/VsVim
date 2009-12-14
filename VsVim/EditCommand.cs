using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VsVim
{
    /// <summary>
    /// Represents a Command in Visual Studio 
    /// </summary>
    internal sealed class EditCommand
    {
        internal readonly KeyInput KeyInput;
        internal readonly EditCommandKind EditCommandKind;
        internal readonly Guid Group;
        internal readonly uint Id;

        /// <summary>
        /// Does this command represent input by the user?
        /// </summary>
        internal bool IsInput
        {
            get
            {
                switch (EditCommandKind)
                {
                    case VsVim.EditCommandKind.Backspace:
                    case VsVim.EditCommandKind.TypeChar:
                    case VsVim.EditCommandKind.Delete:
                    case VsVim.EditCommandKind.Return:
                        return true;
                }
                return false;
            }
        }

        internal EditCommand(
            KeyInput input,
            EditCommandKind kind,
            Guid group,
            uint id)
        {
            KeyInput = input;
            EditCommandKind = kind;
            Group = group;
            Id = id;
        }

        public override string ToString()
        {
            return String.Format("{0} Key.{1}: {2} {3}", EditCommandKind, KeyInput.Key, Group, Id);
        }
    }
}
