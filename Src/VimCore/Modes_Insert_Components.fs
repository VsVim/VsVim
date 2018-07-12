#light

namespace Vim.Modes.Insert
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open Vim

/// Used to track and accumulate the changes to ITextView instance
type internal ITextChangeTracker =

    /// Associated ITextView
    abstract TextView: ITextView

    /// Whether or not change tracking is currently enabled.  Disabling the tracking will
    /// cause the current change to be completed
    abstract TrackCurrentChange: bool with get, set

    /// Whether we should suppress updating last change marks
    abstract SuppressLastChangeMarks: bool with get, set

    /// Current change
    abstract CurrentChange: TextChange option

    /// Complete the current change if there is one
    abstract CompleteChange: unit -> unit

    /// Clear out the current change without completing it
    abstract ClearChange: unit -> unit

    /// Raised when a change is completed
    [<CLIEvent>]
    abstract ChangeCompleted: IDelegateEvent<System.EventHandler<TextChangeEventArgs>>

/// Used to track changes to an individual IVimBuffer
type internal TextChangeTracker
    ( 
        _vimTextBuffer: IVimTextBuffer,
        _textView: ITextView,
        _operations: ICommonOperations
    ) as this =

    static let Key = System.Object()

    let _bag = DisposableBag()
    let _changeCompletedEvent = StandardEvent<TextChangeEventArgs>()

    /// Tracks the current active text change.  This will grow as the user edits
    let mutable _currentTextChange: (TextChange * ITextChange) option = None

    /// Whether or not tracking is currently enabled
    let mutable _trackCurrentChange = false

    /// Whether we should suppress updating last change marks.
    /// Insert mode turns this off and manages the marks itself
    let mutable _suppressLastChangeMarks = false

    do
        // Listen to text buffer change events in order to track edits.  Don't respond to changes
        // while disabled though
        _textView.TextBuffer.Changed
        |> Observable.subscribe (fun args -> this.OnTextChanged args)
        |> _bag.Add

        _textView.Closed 
        |> Event.add (fun _ -> _bag.DisposeAll())

    member x.CurrentChange = 
        match _currentTextChange with
        | None -> None
        | Some (change,_) -> Some change

    member x.TrackCurrentChange
        with get () = _trackCurrentChange
        and set value = 
            if _trackCurrentChange <> value then
                _currentTextChange <- None
                _trackCurrentChange <- value

    /// The change is completed.  Raises the changed event and resets the current text change 
    /// state
    member x.CompleteChange() = 
        match _currentTextChange with
        | None -> 
            ()
        | Some (change, _) -> 
            _currentTextChange <- None
            let args = TextChangeEventArgs(change)
            _changeCompletedEvent.Trigger x args

    /// Clear out the current change without completing it
    member x.ClearChange() =
        _currentTextChange <- None

    /// Convert the ITextChange value into a TextChange instance.  This will not handle any special 
    /// edit patterns and simply does a raw adjustment
    member x.ConvertBufferChange (beforeSnapshot: ITextSnapshot) (change: ITextChange) =

        let convert () = 

            let getDelete () = 
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let isCaretAtStart = 
                    caretPoint.Snapshot = beforeSnapshot &&
                    caretPoint.Position = change.OldPosition

                if isCaretAtStart then
                    TextChange.DeleteRight change.OldLength
                else
                    TextChange.DeleteLeft change.OldLength

            if change.OldText.Length = 0 then
                // This is a straight insert operation
                TextChange.Insert change.NewText
            elif change.NewText.Length = 0 then 
                // This is a straight delete operation
                getDelete()
            else
                // This is a delete + insert combination 
                let left = getDelete()
                let right = TextChange.Insert change.NewText
                TextChange.Combination (left, right)

        // Look for the pattern where tabs are used to replace spaces.  When there are spaces in 
        // the ITextBuffer and tabs are enabled and the user hits <Tab> the spaces will be deleted
        // and replaced with tabs.  The result of the edit though should be recorded as simply 
        // tabs
        if change.OldText.Length > 0 && StringUtil.IsBlanks change.NewText && StringUtil.IsBlanks change.OldText then
            let oldText = _operations.NormalizeBlanks change.OldText
            let newText = _operations.NormalizeBlanks change.NewText
            if newText.StartsWith oldText then
                let diffText = newText.Substring(oldText.Length)
                TextChange.Insert diffText 
            else
                convert ()
        else 
            convert ()

    member x.OnTextChanged (args: TextContentChangedEventArgs) = 

        if x.TrackCurrentChange then
            x.UpdateCurrentChange args

        x.UpdateMarks args

    member x.UpdateCurrentChange (args: TextContentChangedEventArgs) = 

        // At this time we only support contiguous changes (or rather a single change)
        if args.Changes.Count = 1 then
            let newBufferChange = args.Changes.Item(0)
            let newTextChange = x.ConvertBufferChange args.Before newBufferChange

            match _currentTextChange with
            | None -> _currentTextChange <- Some (newTextChange, newBufferChange)
            | Some (oldTextChange, oldBufferChange) -> x.MergeChange oldTextChange oldBufferChange newTextChange newBufferChange 
        else
            x.CompleteChange()

    /// Update the last edit point and last change marks based on the latest change to the
    /// ITextBuffer.  Note that this isn't necessarily a vim originated edit.  Can be done
    /// by another Visual Studio operation but we still treat it like a Vim edit.
    member x.UpdateMarks (args: TextContentChangedEventArgs) = 

        if args.Changes.Count = 1 then
            let change = args.Changes.Item(0)
            let position = 
                if change.Delta > 0 && change.NewEnd > 0 then
                    change.NewEnd - 1
                else
                    change.NewPosition
            let point = SnapshotPoint(args.After, position)
            _vimTextBuffer.LastEditPoint <- Some point

            // If we not suppressing change marks, automatically update the
            // last change start and end positions.
            if not _suppressLastChangeMarks then
                let oldSpan = SnapshotSpan(args.Before, change.OldSpan)
                let newSpan = SnapshotSpan(args.After, change.NewSpan)
                _operations.RecordLastChange oldSpan newSpan
        else
            // When there are multiple changes it is usually the result of a projection 
            // buffer edit coming from a web page edit.  For now that's unsupported
            _vimTextBuffer.LastEditPoint <- None

    /// Attempt to merge the change operations together
    member x.MergeChange oldTextChange (oldChange: ITextChange) newTextChange (newChange: ITextChange) =

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
            let mergedChange = TextChange.CreateReduced oldTextChange newTextChange 
            _currentTextChange <- Some (mergedChange, newChange)
        else
            // If we can't merge then the previous change is complete and we can switch our focus to 
            // the new TextChange value
            x.CompleteChange()
            _currentTextChange <- Some (newTextChange, newChange)

    static member GetTextChangeTracker (bufferData: IVimBufferData) (commonOperationsFactory: ICommonOperationsFactory) =
        let textView = bufferData.TextView
        textView.Properties.GetOrCreateSingletonProperty(Key, (fun () -> 
            let operations = commonOperationsFactory.GetCommonOperations bufferData
            TextChangeTracker(bufferData.VimTextBuffer, textView, operations)))

    interface ITextChangeTracker with 
        member x.TextView = _textView
        member x.TrackCurrentChange
            with get () = x.TrackCurrentChange
            and set value = x.TrackCurrentChange <- value
        member x.SuppressLastChangeMarks
            with get () = _suppressLastChangeMarks
            and set value = _suppressLastChangeMarks <- value
        member x.CurrentChange = x.CurrentChange
        member x.CompleteChange () = x.CompleteChange ()
        member x.ClearChange () = x.ClearChange ()
        [<CLIEvent>]
        member x.ChangeCompleted = _changeCompletedEvent.Publish
