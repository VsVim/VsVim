using System;
using Vim;

namespace VsVim
{
    /// <summary>
    /// Represents a Command in Visual Studio 
    /// </summary>
    internal sealed class EditCommand
    {
        private readonly KeyInput _keyInput;
        internal readonly EditCommandKind EditCommandKind;
        internal readonly Guid Group;
        internal readonly uint Id;

        /// <summary>
        /// Does this EditCommand have a corresponding KeyInput value which should
        /// be used for the EditCommand. Anything returned here should participate in
        /// key mapping.  
        ///
        /// Undo / Redo interception intentionally does not participate in this because 
        /// we don't want to let key mapping interfere with undo / redo when the user
        /// is just clicking on the undo / redo button
        /// </summary>
        internal bool HasKeyInput
        {
            get { return _keyInput != KeyInput.DefaultValue; }
        }

        internal KeyInput KeyInput
        {
            get
            {
                Contract.Requires(HasKeyInput);
                return _keyInput;
            }
        }

        internal bool IsUndo
        {
            get { return EditCommandKind == EditCommandKind.Undo; }
        }

        internal bool IsRedo
        {
            get { return EditCommandKind == EditCommandKind.Redo; }
        }

        internal bool IsPaste
        {
            get { return EditCommandKind == VsVim.EditCommandKind.Paste; }
        }

        internal EditCommand(
            KeyInput input,
            EditCommandKind kind,
            Guid group,
            uint id)
        {
            _keyInput = input;
            EditCommandKind = kind;
            Group = group;
            Id = id;
        }

        public override string ToString()
        {
            return String.Format("{0} Key.{1}: {2} {3}", EditCommandKind, _keyInput.Key, Group, Id);
        }
    }
}
