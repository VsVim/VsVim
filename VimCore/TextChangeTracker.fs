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
        _textView : ITextView,
        _operations : ICommonOperations
    ) as this =

    let _bag = DisposableBag()
    let _changeCompletedEvent = Event<TextChange>()

    /// Tracks the current active text change.  This will grow as the user edits
    let mutable _currentTextChange : (TextChange * ITextChange) option = None

    /// Whether or not tracking is currently enabled
    let mutable _enabled = false

    do
        // Listen to text buffer change events in order to track edits.  Don't respond to changes
        // while disabled though
        _textView.TextBuffer.Changed
        |> Observable.filter (fun _ -> _enabled)
        |> Observable.subscribe (fun args -> this.OnTextChanged args)
        |> _bag.Add

        _textView.Closed 
        |> Event.add (fun _ -> _bag.DisposeAll())

    member x.CurrentChange = 
        match _currentTextChange with
        | None -> None
        | Some (change,_) -> Some change

    member x.Enabled 
        with get () = _enabled
        and set value = 
            if _enabled <> value then
                _currentTextChange <- None
                _enabled <- value

    /// The change is completed.  Raises the changed event and resets the current text change 
    /// state
    member x.CompleteChange() = 
        match _currentTextChange with
        | None -> 
            ()
        | Some (change,_) -> 
            _currentTextChange <- None
            _changeCompletedEvent.Trigger change

    /// Clear out the current change without completing it
    member x.ClearChange() =
        _currentTextChange <- None

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
            x.CompleteChange()

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
            x.CompleteChange()
            _currentTextChange <- Some (newTextChange, newChange)

    interface ITextChangeTracker with 
        member x.TextView = _textView
        member x.Enabled 
            with get () = x.Enabled
            and set value = x.Enabled <- value
        member x.CurrentChange = x.CurrentChange
        member x.CompleteChange () = x.CompleteChange ()
        member x.ClearChange () = x.ClearChange ()
        [<CLIEvent>]
        member x.ChangeCompleted = _changeCompletedEvent.Publish

[<Export(typeof<ITextChangeTrackerFactory>)>]
type internal TextChangeTrackerFactory 
    [<ImportingConstructor>]
    (
        _commonOperationsFactory : ICommonOperationsFactory
    )  =

    let _key = System.Object()
    
    interface ITextChangeTrackerFactory with
        member x.GetTextChangeTracker (bufferData : IVimBufferData) =
            let textView = bufferData.TextView
            textView.Properties.GetOrCreateSingletonProperty(_key, (fun () -> 
                let operations = _commonOperationsFactory.GetCommonOperations bufferData
                TextChangeTracker(textView, operations) :> ITextChangeTracker))
