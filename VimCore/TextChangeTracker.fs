#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Used to track changes to an individual IVimBuffer
type internal TextChangeTracker
    ( 
        _buffer : IVimBuffer,
        _keyboard : IKeyboardDevice,
        _mouse : IMouseDevice ) as this =

    let _bag = DisposableBag()
    let _changeCompletedEvent = Event<string>()

    /// Tracks the current active text change.  This will grow as the user edits
    let mutable _currentTextChange : Span option = None

    do
        // Listen to the events which are relevant to changes.  Make sure to undo them when 
        // the buffer closes

        // Ignore changes which do not happen on focused windows.  Doing otherwise allows
        // random tools to break this feature.  
        // 
        // We also cannot process a text change while we are processing input.  Otherwise text
        // changes which are made as part of a command will be processed as user input.  This 
        // breaks the "." operator
        _buffer.TextBuffer.Changed 
        |> Observable.filter (fun _ -> _buffer.TextView.HasAggregateFocus || _buffer.ModeKind = ModeKind.Insert)
        |> Observable.filter (fun _ -> not _buffer.IsProcessingInput)
        |> Observable.subscribe (fun args -> this.OnTextChanged args)
        |> _bag.Add

        // Caret changes can end an edit
        _buffer.TextView.Caret.PositionChanged
        |> Observable.subscribe (fun _ -> this.OnCaretPositionChanged() )
        |> _bag.Add

        // Mode switches end the current edit
        _buffer.SwitchedMode 
        |> Observable.subscribe (fun _ -> this.ChangeCompleted())
        |> _bag.Add

        _buffer.Closed |> Event.add (fun _ -> _bag.DisposeAll())

    member x.CurrentChange = 
        match _currentTextChange with
        | None -> StringUtil.empty
        | Some(span) -> 
            let snapshot = _buffer.TextBuffer.CurrentSnapshot
            if SnapshotUtil.IsSpanValid snapshot span then snapshot.GetText(span)
            else StringUtil.empty

    /// The change is completed.  Raises the changed event and resets the current text change 
    /// state
    member private x.ChangeCompleted() = 
        match _currentTextChange with
        | None -> ()
        | Some(span) -> 
            let data = x.CurrentChange
            _currentTextChange <- None
            _changeCompletedEvent.Trigger data

    member private x.OnTextChanged (args:TextContentChangedEventArgs) = 

        // Also at this time we only support contiguous changes (or rather a single change)
        if args.Changes.Count = 1 then
            let change = args.Changes.Item(0)
            match _currentTextChange with
            | None -> _currentTextChange <- change.NewSpan |> Some
            | Some(span) -> x.MergeChange span change
        else 
            x.ChangeCompleted()

    member private x.MergeChange (span:Span) (change:ITextChange) = 
        if change.Delta > 0 then x.MergeChangeAdd span change
        elif change.Delta = 0 then x.MergeChangeReplace span change
        else x.MergeChangeDelete span change

    member private x.MergeChangeAdd (span:Span) (change:ITextChange) =
        if span.End = change.OldPosition then 
            _currentTextChange <- Span.FromBounds(span.Start, change.NewEnd) |> Some
        else 
            x.ChangeCompleted()
            _currentTextChange <- change.NewSpan |> Some

    member private x.MergeChangeReplace (span:Span) (change:ITextChange) = 
        // Nothing to do yet
        ()

    member private x.MergeChangeDelete (span:Span) (change:ITextChange) = 
        if span.End = change.OldEnd && change.OldLength <= span.Length then 
            _currentTextChange <- Span(span.Start, span.Length - change.OldLength) |> Some
        else 
            x.ChangeCompleted()
            
    /// This is raised when caret changes.  If this is the result of a user click then 
    /// we need to complete the change.
    member private x.OnCaretPositionChanged () = 
        if _mouse.IsLeftButtonPressed then x.ChangeCompleted()
        elif _buffer.ModeKind = ModeKind.Insert then 
            let keyMove = 
                [ VimKey.Left; VimKey.Right; VimKey.Up; VimKey.Down ]
                |> Seq.map (fun k -> KeyInputUtil.VimKeyToKeyInput k)
                |> Seq.filter (fun k -> _keyboard.IsKeyDown k)
                |> SeqUtil.isNotEmpty
            if keyMove then x.ChangeCompleted()
            
    interface ITextChangeTracker with 
        member x.VimBuffer = _buffer
        member x.CurrentChange = x.CurrentChange
        [<CLIEvent>]
        member x.ChangeCompleted = _changeCompletedEvent.Publish

[<Export(typeof<ITextChangeTrackerFactory>)>]
type internal TextChangeTrackerFactory 
    [<ImportingConstructor>]
    (
        _keyboardDevice : IKeyboardDevice,
        _mouseDevice : IMouseDevice )  =

    let _key = System.Object()
    
    interface ITextChangeTrackerFactory with
        member x.GetTextChangeTracker (vimBuffer:IVimBuffer) = 
            vimBuffer.Properties.GetOrCreateSingletonProperty(_key, fun () -> TextChangeTracker(vimBuffer, _keyboardDevice, _mouseDevice) :> ITextChangeTracker )
