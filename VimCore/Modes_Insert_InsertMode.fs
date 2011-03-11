#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System

type CommandFunction = unit -> ProcessResult

type InsertModeData = {

    /// The transaction which is bracketing this Insert Mode operation
    Transaction : IUndoTransaction

    /// If this Insert is a repeat operation this holds the count and 
    /// wether or not a newline should be inserted after the text
    RepeatData : (int * bool) option
}

type internal InsertMode
    ( 
        _buffer : IVimBuffer, 
        _operations : Modes.ICommonOperations,
        _broker : IDisplayWindowBroker, 
        _editorOptions : IEditorOptions,
        _undoRedoOperations : IUndoRedoOperations,
        _textChangeTracker : ITextChangeTracker,
        _isReplace : bool ) as this =

    let _textView = _buffer.TextView
    let _editorOperations = _operations.EditorOperations
    let mutable _commandMap : Map<KeyInput,CommandFunction> = Map.empty
    let mutable _data : InsertModeData option = None
    let mutable _processTextInputCount = 0

    do
        let commands : (string * CommandFunction) list = 
            [
                ("<Esc>", this.ProcessEscape);
                ("<Up>", this.ProcessUp);
                ("<Down>", this.ProcessDown);
                ("<Left>", this.ProcessLeft);
                ("<Right>", this.ProcessRight);
                ("<C-[>", this.ProcessEscape);
                ("<C-d>", this.ProcessShiftLeft)
                ("<C-t>", this.ProcessShiftRight)
                ("<C-o>", this.ProcessNormalModeOneCommand)
            ]

        _commandMap <-
            commands 
            |> Seq.ofList
            |> Seq.map (fun (str,func) -> (KeyNotationUtil.StringToKeyInput str),func)
            |> Map.ofSeq

    member x.IsProcessingTextInput = _processTextInputCount > 0

    /// Is this KeyInput a raw text input item.  Really anything is text input except 
    /// for a few specific items
    member x.IsTextInput (ki : KeyInput) = 
        match ki.Key with
        | VimKey.Enter -> true
        | VimKey.Back -> true
        | VimKey.Delete -> true
        | _ -> Option.isSome ki.RawChar

    /// Process the TextInput value
    member x.ProcessTextInput (ki : KeyInput) = 
        _processTextInputCount <- _processTextInputCount + 1
        try
            let value = 
                match ki.Key with
                | VimKey.Enter -> 
                    _editorOperations.InsertNewLine()
                | VimKey.Back ->
                    _editorOperations.Backspace()
                | VimKey.Delete ->
                    _editorOperations.Delete()
                | _ ->
                    let text = ki.Char.ToString()
                    _editorOperations.InsertText(text)
    
            if value then
                ProcessResult.Handled ModeSwitch.NoSwitch
            else
                ProcessResult.NotHandled
         finally 
            _processTextInputCount <- _processTextInputCount - 1

    /// Process the up command
    member x.ProcessUp () =
        _operations.MoveCaretUp 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Process the down command
    member x.ProcessDown () =
        _operations.MoveCaretDown 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Process the left command
    member x.ProcessLeft () =
        _operations.MoveCaretLeft 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Process the right command
    member x.ProcessRight () =
        _operations.MoveCaretRight 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Enter normal mode for a single command
    member x.ProcessNormalModeOneCommand() =
        let switch = ModeSwitch.SwitchModeWithArgument (ModeKind.Normal, ModeArgument.OneTimeCommand ModeKind.Insert)
        ProcessResult.Handled switch

    /// Process the CTRL-D combination and do a shift left
    member x.ProcessShiftLeft() = 
        let range = TextViewUtil.GetCaretLineRange _textView 1
        _operations.ShiftLineRangeLeft range 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Process the CTRL-T combination and do a shift right
    member x.ProcessShiftRight() = 
        let range = TextViewUtil.GetCaretLineRange _textView 1
        _operations.ShiftLineRangeRight range 1
        ProcessResult.Handled ModeSwitch.NoSwitch

    /// Apply the repeated edits the the ITextBuffer
    member x.MaybeApplyRepeatedEdits () = 
        // Need to close out the edit transaction if there is any
        match _data with
        | None ->
            // nothing to do
            ()
        | Some data ->

            // Start by None'ing out the _transaction variable.  Don't need it anymore
            _data <- None
            try
                match data.RepeatData with 
                | None -> 
                    ()
                | Some (count, addNewLines) ->
                    match _textChangeTracker.CurrentChange with
                    | Some change ->
                        match change with
                        | TextChange.Insert text -> 
                            // Insert the same text 'count - 1' times at the cursor
                            let text = 
                                if addNewLines then
                                    let text = Environment.NewLine + text
                                    StringUtil.repeat (count - 1) text
                                else 
                                    StringUtil.repeat (count - 1) text
    
                            let caretPoint = TextViewUtil.GetCaretPoint _textView
                            let span = SnapshotSpan(caretPoint, 0)
                            let snapshot = _textView.TextBuffer.Replace(span.Span, text) |> ignore
    
                            // Now make sure to position the caret at the end of the inserted
                            // text
                            TextViewUtil.MoveCaretToPosition _textView (caretPoint.Position + text.Length)
                        | TextChange.Delete deleteCount -> 
                            // Delete '(count - 1) * deleteCount' more characters
                            let caretPoint = TextViewUtil.GetCaretPoint _textView
                            let count = deleteCount * (count - 1)
                            let count = min (_textView.TextSnapshot.Length - caretPoint.Position) count
                            _textView.TextBuffer.Delete((Span(caretPoint.Position, count))) |> ignore
    
                            // Now make sure the caret is still at the same position
                            TextViewUtil.MoveCaretToPosition _textView caretPoint.Position
                    | None -> 
                        // Nothing to do if there is no change
                        ()

            finally
                data.Transaction.Complete()

    member x.ProcessEscape () =

        this.MaybeApplyRepeatedEdits()

        if _broker.IsCompletionActive || _broker.IsSignatureHelpActive || _broker.IsQuickInfoActive then
            _broker.DismissDisplayWindows()
            _operations.MoveCaretLeft 1 
            ProcessResult.OfModeKind ModeKind.Normal

        else
            // Need to adjust the caret on exit.  Typically it's just a move left by 1 but if we're
            // in virtual space we just need to get out of it.
            let virtualPoint = TextViewUtil.GetCaretVirtualPoint _textView
            if virtualPoint.IsInVirtualSpace then 
                _operations.MoveCaretToPoint virtualPoint.Position
            else
                _operations.MoveCaretLeft 1 
            ProcessResult.OfModeKind ModeKind.Normal

    /// Can Insert mode handle this particular KeyInput value 
    member x.CanProcess ki = 
        if Map.containsKey ki _commandMap then
            true
        else
            x.IsTextInput ki

    /// Process the KeyInput
    member x.Process ki = 
        match Map.tryFind ki _commandMap with
        | Some(func) -> 
            func()
        | None -> 
            if x.IsTextInput ki then
                x.ProcessTextInput ki
            else
                ProcessResult.NotHandled

    interface IInsertMode with 
        member x.VimBuffer = _buffer
        member x.CommandNames =  _commandMap |> Seq.map (fun p -> p.Key) |> Seq.map OneKeyInput
        member x.ModeKind = if _isReplace then ModeKind.Replace else ModeKind.Insert
        member x.IsProcessingTextInput = x.IsProcessingTextInput
        member x.CanProcess ki = x.CanProcess ki
        member x.IsTextInput ki = x.IsTextInput ki
        member x.Process ki = x.Process ki
        member x.OnEnter arg = 

            // On enter we need to check the 'count' and possibly set up a transaction to 
            // lump edits and their repeats together
            _data <-
                match arg with
                | ModeArgument.InsertWithCount count ->
                    if count > 1 then
                        let transaction = _undoRedoOperations.CreateUndoTransaction "Insert"
                        Some { Transaction = transaction; RepeatData = Some (count, false) }
                    else
                        None
                | ModeArgument.InsertWithCountAndNewLine count ->
                    if count > 1 then
                        let transaction = _undoRedoOperations.CreateUndoTransaction "Insert"
                        Some { Transaction = transaction; RepeatData = Some (count, true) }
                    else
                        None
                | ModeArgument.InsertWithTransaction transaction ->
                    Some { Transaction = transaction; RepeatData = None }
                | _ -> 
                    None

            // If this is replace mode then go ahead and setup overwrite
            if _isReplace then
                _editorOptions.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true)

        member x.OnLeave () = 

            // Ensure the transaction is complete and None'd out.  We can get here with an active
            // transaction if we leave Insert mode via an API call or possibly if an exception
            // happens during processing of the transaction
            try
                match _data with
                | None -> ()
                | Some data -> data.Transaction.Complete()
            finally
                _data <- None

            // If this is replace mode then go ahead and undo overwrite
            if _isReplace then
                _editorOptions.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false)
        member x.OnClose() = ()
