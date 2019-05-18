#light

namespace Vim

open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type internal ModeLineInterpreter =

    /// Constructor for a mode line interpreter
    new: textBuffer: ITextBuffer * localSettings: IVimLocalSettings -> ModeLineInterpreter

    /// Check the contents of the buffer for a modeline
    member CheckModeLine: unit -> string option * string option
