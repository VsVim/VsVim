#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type IVimHost =
    abstract Beep : unit -> unit

    /// Format the provided lines
    abstract FormatLines : ITextView -> SnapshotLineSpan -> unit

    /// Go to the definition of the value under the cursor
    abstract GoToDefinition : unit -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToLocalDeclaration : ITextView -> string -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToGlobalDeclaration : ITextView -> string -> bool

    /// Go to the specified file name
    abstract GoToFile : string -> bool

    /// Go to the matching construct of the value under the cursor
    abstract GoToMatch : unit -> bool

    /// Go to the next tab window
    abstract GoToNextTab : count : int -> unit

    /// Go to the previous tab window
    abstract GoToPreviousTab: count : int -> unit

    abstract GetName : ITextBuffer -> string

    abstract NavigateTo : point : VirtualSnapshotPoint -> bool

    /// Display the open file dialog 
    abstract ShowOpenFileDialog : unit -> unit

    /// Save the current document
    abstract Save : ITextView -> unit

    /// Save the current document as a new file with the specified name
    abstract SaveCurrentFileAs : string -> unit

    /// Saves all files
    abstract SaveAllFiles : unit -> unit

    /// Close the given file
    abstract Close : ITextView -> checkDirty:bool -> unit

    /// Closes all files
    abstract CloseAllFiles : checkDirty:bool -> unit

    /// Close the provided view
    abstract CloseView : ITextView -> checkDirty:bool -> unit

    /// Builds the solution
    abstract BuildSolution : unit -> unit

    /// Split the views
    abstract SplitView : ITextView -> unit

    /// Move to the view above the current one
    abstract MoveViewUp : ITextView -> unit

    /// Move to the view below the current one
    abstract MoveViewDown : ITextView -> unit


