#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

[<RequireQualifiedAccess>]
type HostResult =
    | Success
    | Error of string

type IVimHost =
    abstract Beep : unit -> unit

    /// Ensure that the given point is visible
    abstract EnsureVisible : textView : ITextView -> point : SnapshotPoint -> unit

    /// Format the provided lines
    abstract FormatLines : textView : ITextView -> range : SnapshotLineRange -> unit

    /// Get the ITextView which currently has keyboard focus
    abstract GetFocusedTextView : unit -> ITextView option

    /// Go to the definition of the value under the cursor
    abstract GoToDefinition : unit -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToLocalDeclaration : textView : ITextView -> identifier : string -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToGlobalDeclaration : tetxView : ITextView -> identifier : string -> bool

    /// Go to the "count" next tab window in the specified direction.  This will wrap 
    /// around
    abstract GoToNextTab : Path -> count : int -> unit

    /// Go the nth tab.  The first tab can be accessed with both 0 and 1
    abstract GoToTab : index : int -> unit

    /// Get the name of the given ITextBuffer
    abstract GetName : textBuffer : ITextBuffer -> string

    /// Is the ITextBuffer in a dirty state?
    abstract IsDirty : textBuffer : ITextBuffer -> bool

    /// Is the ITextBuffer readonly
    abstract IsReadOnly : textBuffer : ITextBuffer -> bool

    /// Is the ITextView visible to the user
    abstract IsVisible : textView : ITextView -> bool

    /// Loads the new file into the existing window
    abstract LoadFileIntoExistingWindow : filePath : string -> textBuffer : ITextBuffer -> HostResult

    /// Loads the new file into a new existing window
    abstract LoadFileIntoNewWindow : filePath : string -> HostResult

    /// Run the host specific make operation
    abstract Make : jumpToFirstError : bool -> arguments : string -> HostResult

    /// Move to the view above the current one
    abstract MoveViewUp : ITextView -> unit

    /// Move to the view below the current one
    abstract MoveViewDown : ITextView -> unit

    /// Move to the view to the right of the current one
    abstract MoveViewRight : ITextView -> unit

    /// Move to the view to the right of the current one
    abstract MoveViewLeft : ITextView -> unit

    abstract NavigateTo : point : VirtualSnapshotPoint -> bool

    /// Quit the application
    abstract Quit : unit -> unit

    /// Display the open file dialog 
    abstract ShowOpenFileDialog : unit -> unit

    /// Reload the contents of the ITextBuffer discarding any changes
    abstract Reload : ITextBuffer -> bool

    /// Save the provided ITextBuffer instance
    abstract Save : ITextBuffer -> bool 

    /// Save the current document as a new file with the specified name
    abstract SaveTextAs : text:string -> filePath:string -> bool 

    /// Close the provided view
    abstract Close : ITextView -> unit

    /// Split the views horizontally
    abstract SplitViewHorizontally : ITextView -> HostResult

    /// Split the views horizontally
    abstract SplitViewVertically: ITextView -> HostResult

    /// Raised when the visibility of an ITextView changes
    [<CLIEvent>]
    abstract IsVisibleChanged : IEvent<ITextView>

module internal VimHostExtensions =
    type IVimHost with 
        member x.SaveAs (textView:ITextView) filePath = 
            x.SaveTextAs (textView.TextSnapshot.GetText()) filePath

