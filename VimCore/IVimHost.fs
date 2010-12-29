#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type IVimHost =
    abstract Beep : unit -> unit

    /// Format the provided lines
    abstract FormatLines : ITextView -> SnapshotLineRange -> unit

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

    /// Ensure that the given point is visible
    abstract EnsureVisible : ITextView -> SnapshotPoint -> unit

    abstract NavigateTo : point : VirtualSnapshotPoint -> bool

    /// Display the open file dialog 
    abstract ShowOpenFileDialog : unit -> unit

    /// Save the current document
    abstract Save : ITextView -> bool 

    /// Save the current document as a new file with the specified name
    abstract SaveTextAs : text:string -> filePath:string -> bool 

    /// Saves all files
    abstract SaveAllFiles : unit -> bool

    /// Closes all files
    abstract CloseAllFiles : checkDirty:bool -> unit

    /// Close the provided view
    abstract Close : ITextView -> checkDirty:bool -> unit

    /// Builds the solution
    abstract BuildSolution : unit -> unit

    /// Split the views
    abstract SplitView : ITextView -> unit

    /// Move to the view above the current one
    abstract MoveViewUp : ITextView -> unit

    /// Move to the view below the current one
    abstract MoveViewDown : ITextView -> unit


module internal VimHostExtensions =
    type IVimHost with 
        member x.SaveAs (textView:ITextView) filePath = 
            x.SaveTextAs (textView.TextSnapshot.GetText()) filePath

