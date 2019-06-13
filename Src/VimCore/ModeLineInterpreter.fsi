#light

namespace Vim

open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type internal ModeLineInterpreter =

    /// Constructor for a mode line interpreter
    new: textBuffer: ITextBuffer * localSettings: IVimLocalSettings -> ModeLineInterpreter

    /// Check the contents of the buffer for a modeline, returning a tuple of
    /// the line we used as a modeline, if any, and a string representing the
    /// first sub-option that produced an error if any
    member CheckModeLine: IVimWindowSettings -> string option * string option
