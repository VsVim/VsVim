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
        _operations : ICommonOperations,
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
        |> Observable.filter (fun _ -> (not _buffer.IsProcessingInput) || _buffer.InsertMode.IsProcessingDirectInsert || _buffer.ReplaceMode.IsProcessingDirectInsert)
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
    member x.ChangeCompleted() = 
        match _currentTextChange with
        | None -> 
            ()
        | Some (change,_) -> 
            _currentTextChange <- None
            _changeCompletedEvent.Trigger change

    /// Convert the ITextChange value into a TextChange instance.  This will not handle any special 
    /// edit patterns and simply does a raw adjustment
    member x.ConvertBufferChange (change : ITextChange) =
        if change.OldText.Length = 0 then
            // This is a straight insert operation
            TextChange.Insert change.NewText
        elif change.NewText.Length = 0 then 
            // This is a straight delete operation
            TextChange.Delete change.OldText.Length
        else
            // This is a delete + insert combination 
            let left = TextChange.Delete change.OldText.Length
            let right = TextChange.Insert change.NewText
            TextChange.Combination (left, right)

    /// Looks for special edit patterns in the buffer and creates an TextChange value for those
    /// specific patterns
    member x.ConvertSpecialBufferChange (change : ITextChange) = 

        // Look for the pattern where tabs are used to replace spaces.  When there are spaces in 
        // the ITextBuffer and tabs are enabled and the user hits <Tab> the spaces will be deleted
        // and replaced with tabs.  The result of the edit though should be recorded as simply 
        // tabs
        if change.OldText.Length > 0 && StringUtil.isBlanks change.NewText && StringUtil.isBlanks change.OldText then
            let oldText = _operations.NormalizeBlanks change.OldText
            let newText = _operations.NormalizeBlanks change.NewText
            if newText.StartsWith oldText then
                let diffText = newText.Substring(oldText.Length)
                TextChange.Insert diffText |> Some
            else
                None
        else 
            None

    member x.OnTextChanged (args : TextContentChangedEventArgs) = 

        // At this time we only support contiguous changes (or rather a single change)
        if args.Changes.Count = 1 then
            let newBufferChange = args.Changes.Item(0)
            let newTextChange = 
                match x.ConvertSpecialBufferChange newBufferChange with
                | None -> x.ConvertBufferChange newBufferChange
                | Some textChange -> textChange

            match _currentTextChange with
            | None -> _currentTextChange <- Some (newTextChange, newBufferChange)
            | Some (oldTextChange, oldBufferChange) -> x.MergeChange oldTextChange oldBufferChange newTextChange newBufferChange 
        else
            x.ChangeCompleted()

    /// Attempt to merge the change operations together
    member x.MergeChange oldTextChange (oldChange : ITextChange) newTextChange (newChange : ITextChange) =

        // First step is to determine if we can merge the changes.  Essentially we need to ensure that
        // the changes are occurring at the same point in the ITextBuffer 
        let canMerge = 
            if newChange.OldText.Length = 0 then 
                // This is a pure insert so it must occur at the end of the last ITextChange
                // in order to be valid 
                oldChange.NewEnd = newChange.OldPosition
            elif newChange.NewText.Length = 0 then 
                // This is a pure delete operation.  The delete must start from the end of the 
                // previous change 
                oldChange.NewEnd = newChange.OldEnd
            elif newChange.Delta >= 0 && oldChange.NewEnd = newChange.OldPosition then
                // Replace just after the previous edit
                true
            else
                // This is a replace operation.  Easiest way to check is to see if the delta applied
                // to the previous end puts us in the correct end position
                oldChange.NewEnd - newChange.OldText.Length = newChange.OldPosition

        if canMerge then 
            // Change is legal.  Merge it with the existing one and move on
            let mergedChange = TextChange.Merge oldTextChange newTextChange 
            _currentTextChange <- Some (mergedChange, newChange)
        else
            // If we can't merge then the previous change is complete and we can switch our focus to 
            // the new TextChange value
            x.ChangeCompleted()
            _currentTextChange <- Some (newTextChange, newChange)


    /// This is raised when caret changes.  If this is the result of a user click then 
    /// we need to complete the change.
    member x.OnCaretPositionChanged () = 
        if _mouse.IsLeftButtonPressed then 
            x.ChangeCompleted()
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
        _mouseDevice : IMouseDevice,
        _commonOperationsFactory : ICommonOperationsFactory
    )  =

    let _key = System.Object()
    
    interface ITextChangeTrackerFactory with
        member x.GetTextChangeTracker (vimBuffer : IVimBuffer) = 
            vimBuffer.Properties.GetOrCreateSingletonProperty(_key, fun () -> 
                let operations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
                TextChangeTracker(vimBuffer, operations, _keyboardDevice, _mouseDevice) :> ITextChangeTracker )
