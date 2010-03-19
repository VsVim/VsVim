#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type IVimHost =
    abstract Beep : unit -> unit
    abstract OpenFile : string -> unit
    abstract Undo : ITextBuffer -> count:int -> unit
    abstract Redo : ITextBuffer -> count:int -> unit

    /// Go to the definition of the value under the cursor
    abstract GoToDefinition : unit -> bool

    /// Go to the matching construct of the value under the cursor
    abstract GoToMatch : unit -> bool

    abstract GetName : ITextBuffer -> string
    abstract NavigateTo : point : VirtualSnapshotPoint -> bool

    /// Display the open file dialog 
    abstract ShowOpenFileDialog : unit -> unit

    /// Save the current document
    abstract SaveCurrentFile : unit -> unit

    /// Save the current document as a new file with the specified name
    abstract SaveCurrentFileAs : string -> unit

    /// Close the current file
    abstract CloseCurrentFile : checkDirty:bool -> unit



