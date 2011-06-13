
namespace VsVim
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
        /// This command represents the user clicking on the undo button
        /// </summary>
        Undo,

        /// <summary>
        /// This command represents the user clicking on the redo button
        /// </summary>
        Redo
    }
}
