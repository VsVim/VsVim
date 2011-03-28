
namespace VsVim
{
    /// <summary>
    /// Kind of Command in Visual Studio.  It's an attempt to unify the different command groups which 
    /// are in different versions of Visual Studio to which VsVim is concerned about
    /// </summary>
    internal enum EditCommandKind
    {
        Unknown,
        TypeChar,
        Return,
        Cancel,
        Delete,
        Backspace,
        CursorMovement,
        Undo,
        Redo
    }
}
