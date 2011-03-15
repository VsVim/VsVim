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
    let _changeCompletedEvent = Event<TextChange>()

    /// Tracks the current active text change.  This will grow as the user edits
    let mutable _currentTextChange : (TextChange * ITextChange) option = None

    do
        // Listen to the events which are relevant to changes.  Make sure to undo them when 
        // the buffer closes

        // Ignore changes which do not happen on focused windows.  Doing otherwise allows
        // random tools to break this feature.  
        // 
        // We also cannot process a text change while we are processing input.  Otherwise text
        // changes which are made as part of a command will be processed as user input.  This 
        // breaks the "." operator.  The one exception is the processing of text input which
        // signifies a user change
        //
        // TODO: Maybe the above would be better served by just checking to see if we're in a
        // repeat and logging based on that
        _buffer.TextBuffer.Changed 
        |> Observable.filter (fun _ -> _buffer.TextView.HasAggregateFocus || _buffer.ModeKind = ModeKind.Insert || _buffer.ModeKind = ModeKind.Replace)
        |> Observable.filter (fun _ -> (not _buffer.IsProcessingInput) || _buffer.InsertMode.IsProcessingTextInput || _buffer.ReplaceMode.IsProcessingTextInput)
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
        | None -> None
        | Some(change,_) -> Some change

    /// The change is completed.  Raises the changed event and resets the current text change 
    /// state
    member private x.ChangeCompleted() = 
        match _currentTextChange with
        | None -> ()
        | Some(change,_) -> 
            _currentTextChange <- None
            _changeCompletedEvent.Trigger change

    member private x.OnTextChanged (args:TextContentChangedEventArgs) = 

        // Also at this time we only support contiguous changes (or rather a single change)
        if args.Changes.Count = 1 then
            let change = args.Changes.Item(0)
            let opt = 
                if change.Delta > 0 then 
                    Some (TextChange.Insert change.NewText)
                elif change.Delta < 0 then 
                    Some (TextChange.Delete change.OldLength)
                else 
                    // Really this is a replace but for the purpose of change we model it
                    // as Insert.  Could as easily be modeled as Replace (and possibly 
                    // should but don't need the flexibility right now)
                    Some (TextChange.Insert change.NewText)
            match opt with 
            | None -> ()
            | Some(textChange) ->
                match _currentTextChange with
                | None -> _currentTextChange <- Some(textChange, change)
                | Some(oldTextChange,oldRawChange) -> x.MergeChange oldTextChange oldRawChange textChange change 
            else 
                x.ChangeCompleted()

    member private x.MergeChange oldTextChange (oldChange:ITextChange) newTextChange (newChange:ITextChange) =

        // Right now we only support merging Insert with insert and delete with delete
        match oldTextChange, newTextChange with
        | TextChange.Insert(t1), TextChange.Insert(t2) -> 
            if oldChange.NewEnd = newChange.OldPosition then 
                _currentTextChange <- Some ((TextChange.Insert (t1 + t2)), newChange)
            else
                x.ChangeCompleted()
                _currentTextChange <- Some (newTextChange,newChange)
        | TextChange.Delete(len1), TextChange.Delete(len2) -> 
            if oldChange.NewPosition - 1= newChange.OldPosition then
                _currentTextChange <- Some ((TextChange.Delete (len1+len2)), newChange)
            else
                x.ChangeCompleted()
                _currentTextChange <- Some (newTextChange, newChange)
        | TextChange.Insert(t1), TextChange.Delete(len1) -> 
            if oldChange.NewEnd - 1 = newChange.OldPosition then
                let newLength = t1.Length - len1
                if newLength > 0 then 
                    let text = t1.Substring(0,newLength)
                    _currentTextChange <- Some ((TextChange.Insert(text), newChange))
                else
                    x.ChangeCompleted()
            else
                x.ChangeCompleted()
                _currentTextChange <- Some (newTextChange, newChange)
        | TextChange.Delete(_), TextChange.Insert(_) -> 
            () // Not supported yet

    /// This is raised when caret changes.  If this is the result of a user click then 
    /// we need to complete the change.
    member private x.OnCaretPositionChanged () = 
        if _mouse.IsLeftButtonPressed then x.ChangeCompleted()
        elif _buffer.ModeKind = ModeKind.Insert then 
            let keyMove = 
                [ VimKey.Left; VimKey.Right; VimKey.Up; VimKey.Down ]
                |> Seq.map (fun k -> KeyInputUtil.VimKeyToKeyInput k)
                |> Seq.filter (fun k -> _keyboard.IsKeyDown k.Key)
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
