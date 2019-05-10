using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio
{
    /// <summary>
    /// This interface facilitates the unit testing of operations that involve
    /// command dispatching, like executing 'Edit.GoToDefinition' in the
    /// appropriate way for a specified text view
    /// </summary>
    internal interface ICommandDispatcher
    {
        bool ExecuteCommand(ITextView textview, string command, string args, bool postCommand);
    }
}
