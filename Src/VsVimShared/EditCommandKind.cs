
namespace Vim.VisualStudio
{
    /// <summary>
    /// Kind of Command in Visual Studio.  It's an attempt to unify the different command groups which 
    /// are in different versions of Visual Studio to which VsVim is concerned about
    /// </summary>
    internal enum EditCommandKind
    {
        /// <summary>
        /// This command represents user input
        /// </summary>
        UserInput,

        /// <summary>
        /// This command represents a visual studio command.  In reality everything is a Visual Studio 
        /// command.  However these represent actions which should not be interpreted by VsVim and instead
        /// left to the control of Visual Studio.  All of these key sequences can go back to VsVim
        /// control if the user unmaps the keys that produced them
        /// </summary>
        VisualStudioCommand,

        /// <summary>
        /// This command represents the user clicking on the undo button
        /// </summary>
        Undo,

        /// <summary>
        /// This command represents the user clicking on the redo button
        /// </summary>
        Redo,

        /// <summary>
        /// The goto definition command
        /// </summary>
        GoToDefinition,

        /// <summary>
        /// Comment the selection
        /// </summary>
        Comment,

        /// <summary>
        /// Uncomment the selection
        /// </summary>
        Uncomment,

        /// <summary>
        /// The paste command
        /// </summary>
        Paste,
    }
}
