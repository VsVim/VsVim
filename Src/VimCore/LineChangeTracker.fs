#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Data relating to tracking changes to a line
type internal LineChangeTrackingData =
    {
        LineNumber : int
        SavedLine : string option
    }
    static member Empty = {
        LineNumber = 0
        SavedLine = None
    }

/// Used to track changes to the current line of an individual IVimBuffer
type internal LineChangeTracker
    ( 
        _vimBufferData : IVimBufferData
    ) as x =

    let _disposables = DisposableBag()
    let _textView = _vimBufferData.TextView
    let _textBuffer = _textView.TextBuffer
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let mutable _currentData = LineChangeTrackingData.Empty
    let mutable _changedData = LineChangeTrackingData.Empty

    do
        x.Initialize()

    member x.TextView = _textView
    member x.TextBuffer = _textBuffer
    member x.UndoRedoOperations = _undoRedoOperations
    member x.CaretLine = TextViewUtil.GetCaretLine _textView
    member x.Disposables = _disposables

    member x.CurrentData
        with get() = _currentData
        and set value = _currentData <- value
    member x.ChangedData
        with get() = _changedData
        and set value = _changedData <- value

    member x.Initialize () =

        // Listen to caret changes to detect line changes
        x.TextView.Caret.PositionChanged
        |> Observable.subscribe (fun _ -> x.OnCaretPositionChanged())
        |> x.Disposables.Add

        // Listen to text buffer change events in order to track edits
        x.TextView.TextBuffer.Changed
        |> Observable.subscribe (fun args -> x.OnTextChanged args)
        |> x.Disposables.Add

        // Unregister all our handles when the text view is closed
        x.TextView.Closed 
        |> Event.add (fun _ -> x.Disposables.DisposeAll())

        // Record caret position on entry.
        x.OnCaretPositionChanged()

    /// Handler for caret position changes
    member x.OnCaretPositionChanged () = 
        let caretLineNumber = x.CaretLine.LineNumber
        if caretLineNumber <> x.CurrentData.LineNumber then
            x.CurrentData <- {
                LineNumber = caretLineNumber
                SavedLine = x.CaretLine.GetText() |> Some
            }

    /// Handler for text changes
    member x.OnTextChanged args = 
        x.ChangedData <-
            let caretLineNumber = x.CaretLine.LineNumber
            if caretLineNumber = x.CurrentData.LineNumber then
                x.CurrentData
            else
                LineChangeTrackingData.Empty

    /// Swap the most recently changed line with its saved copy
    member x.Swap () =
        match x.ChangedData.SavedLine with
        | Some savedLine ->
            let lineNumber = x.ChangedData.LineNumber
            let span = x.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent
            let changedLine = span.GetText()
            x.UndoRedoOperations.EditWithUndoTransaction "Undo Line" x.TextView <| fun () ->
                x.TextBuffer.Delete span.Span |> ignore
                x.TextBuffer.Insert(span.Start.Position, savedLine) |> ignore
                TextViewUtil.MoveCaretToPosition x.TextView span.Start.Position
            x.ChangedData <- {
                LineNumber = lineNumber
                SavedLine = Some changedLine
            }
            true
        | None ->
            false

    /// Implement the ILineChangeTracker interface
    interface ILineChangeTracker with 
        member x.Swap() = x.Swap()

[<Export(typeof<ILineChangeTrackerFactory>)>]
type internal LineChangeTrackerFactory 
    [<ImportingConstructor>]
    (
    )  =

    let _key = System.Object()
    
    interface ILineChangeTrackerFactory with
        member x.GetLineChangeTracker (bufferData : IVimBufferData) =
            let textView = bufferData.TextView
            textView.Properties.GetOrCreateSingletonProperty(_key, (fun () -> 
                LineChangeTracker(bufferData) :> ILineChangeTracker))
