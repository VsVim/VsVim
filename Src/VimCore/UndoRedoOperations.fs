#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Collections.Generic

type UndoTransaction 
    (
        _name : string,
        _transaction : ITextUndoTransaction option
    ) =

    interface IUndoTransaction with
        member x.Complete () = 
            VimTrace.TraceInfo("Complete Undo Transaction: {0}", _name)
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Complete()
        member x.Cancel() = 
            VimTrace.TraceInfo("Cancel Undo Transaction: {0}", _name)
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Cancel()
        member x.Dispose() = 
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Dispose()

type TextViewUndoTransaction 
    (
        _name : string,
        _transaction : ITextUndoTransaction option,
        _editorOperations : IEditorOperations option
    ) =
    inherit UndoTransaction(_name, _transaction)

    interface ITextViewUndoTransaction with
        member x.AddAfterTextBufferChangePrimitive() =
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddAfterTextBufferChangePrimitive()
        member x.AddBeforeTextBufferChangePrimitive() = 
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddBeforeTextBufferChangePrimitive()

/// Information about how many actual undo / redo operations need
/// to be performed to do a single Vim undo 
[<RequireQualifiedAccess>]
type UndoRedoData =

    /// The stack contains 'count' normal undo / redo transactions which line up
    /// with Vim behavior.  The count is kept here instead of having a stack of 'count'
    /// depth in order to keep memory allocations minimal.
    | Normal of int

    /// The stack contains 'count' normal undo / redo transactions which line up 
    /// with a single Vim undo / redo transaction
    | Linked of int 

/// This is a method for linking together several simple undo transactions into a
/// single undo unit.  Undo or Redo of this item will undo / redo every undo unit
/// that it is linked to
type LinkedUndoTransaction
    (
        _name : string,
        _undoRedoOperations : UndoRedoOperations
    ) = 

    let mutable _isComplete = false

    member x.Complete () = 
        if not _isComplete then
            VimTrace.TraceInfo("Complete Linked Undo Transaction: {0}", _name)
            _isComplete <- true
            _undoRedoOperations.LinkedUndoTransactionClosed x

    interface ILinkedUndoTransaction with
        member x.Complete() = x.Complete()
        member x.Dispose () = x.Complete()

/// Handle the undo and redo behavior for the IVimBuffer.  This class is very tolerant
/// of unexpected behavior.  The goal here is to layer Vim undo / redo behavior on top of
/// Visual Studio undo / redo behavior.  This is not a goal that can be achieved in a 
/// rock solid fashion as undo / redo in Visual Studio is a very ... temperamental 
/// technology.  As much as we can layer on top of it, people can still poke at the lower
/// layers in unexpected ways.  If this happens we should revert back to Visual Studio
/// undo rather than throwing and killing the ITextBuffer.  
and UndoRedoOperations 
    (
        _statusUtil : IStatusUtil,
        _textUndoHistory : ITextUndoHistory option,
        _editorOperationsFactoryService : IEditorOperationsFactoryService
    ) as this =

    let mutable _linkedUndoTransactionStack = Stack<LinkedUndoTransaction>()
    let mutable _inUndoRedo = false
    let mutable _undoStack : UndoRedoData list = List.empty
    let mutable _redoStack : UndoRedoData list = List.empty
    let _bag = DisposableBag()

    do
        match _textUndoHistory with 
        | None -> ()
        | Some textUndoHistory -> 
            textUndoHistory.UndoTransactionCompleted
            |> Observable.filter (fun args ->
                
                // We are only concerned with added transactions as they affect the actual undo
                // stack.  Merged transactions don't affect the stack
                //
                // Note: This code doesn't actually do anything in Visual Studio 2010.  Merged
                // transactions are simply not reported at all.  Be safe here though in case
                // other implementations do
                match args.Result with 
                | TextUndoTransactionCompletionResult.TransactionAdded -> true
                | TextUndoTransactionCompletionResult.TransactionMerged -> false
                | _ -> false)
            |> Observable.subscribe (fun _ -> this.OnUndoTransactionCompleted())
            |> _bag.Add

            textUndoHistory.UndoRedoHappened
            |> Observable.subscribe (fun _ -> this.OnUndoRedoHappened())
            |> _bag.Add

    /// Are we in the middle of a linked undo transaction
    member x.InLinkedUndoTransaction = _linkedUndoTransactionStack.Count > 0

    member x.LinkedUndoTransactionStack = _linkedUndoTransactionStack

    member x.UndoStack = _undoStack

    member x.RedoStack = _redoStack

    /// Closing simply means we need to detach from our event handlers so memory can
    /// be reclaimed.  IVimBuffer operates at an ITextView level but here we are 
    /// handling events at an ITextBuffer level.  This easily creates a leak if we 
    /// remain attached and only the ITextView is closed.  
    member x.Close () = _bag.DisposeAll()

    /// Add 'count' normal undo transactions to the top of the stack
    member x.AddToStackNormal count list =
        match list with
        | [] -> [ UndoRedoData.Normal count ]
        | head :: tail ->
            let head, tail = 
                match head with
                | UndoRedoData.Normal c -> UndoRedoData.Normal (count + c), tail
                | UndoRedoData.Linked 0 -> UndoRedoData.Normal count, tail
                | UndoRedoData.Linked _ -> UndoRedoData.Normal count, list
            head :: tail 

    /// This method is called when the undo / redo management detects we have gotten into
    /// a bad state.  Essentially we've lost our ability to sync with the editor undo
    /// stack.  Reset all state at this point so that undo / redo goes back to the normal
    /// editor functions.  Edits after this point will sync back up with us
    member x.ResetState() =
        _linkedUndoTransactionStack.Clear()
        _undoStack <- List.empty
        _redoStack <- List.empty

    /// If undo / redo is called when a linked undo transaction is open then we need to take
    /// action.  Tear down the vim undo / redo stacks and revert back to Visual Studio undo
    /// behavior
    ///
    /// Note: Any time this occurs is assuredly a bug in code.  It's important we clean up any
    /// instances of this.  Normally we'd not have compensating code here and instead would opt
    /// to say we should be fixing the bug instead.  However the results of the bug are very 
    /// bad.  All work, potentially hours of it, which is made after the bug occurs will be undone
    /// after the very next single undo operation. 
    member x.CheckForBrokenUndoRedoChain() =
        if _linkedUndoTransactionStack.Count > 0 then 
            x.ResetState()
            VimTrace.TraceInfo("!!! Broken undo / redo chain")
            _statusUtil.OnError Resources.Common_UndoChainBroken

    member x.CreateUndoTransaction (name : string) = 
        VimTrace.TraceInfo("Open Undo Transaction: {0}", name)
        match _textUndoHistory with
        | None -> 
            new UndoTransaction(name, None) :> IUndoTransaction
        | Some textUndoHistory ->
            let transaction = textUndoHistory.CreateTransaction(name)
            new UndoTransaction(name, Some transaction) :> IUndoTransaction

    member x.CreateTextViewUndoTransaction (name : string) (textView : ITextView) = 
        VimTrace.TraceInfo("Open Text View Undo Transaction: {0}", name)
        match _textUndoHistory with
        | None -> 
            // Don't support either if the textUndoHistory is not present.  While we have an instance
            // of IEditorOperations it will still fail because internally it's trying to access
            // the same ITextUndorHistory which is null
            new TextViewUndoTransaction(name, None, None) :> ITextViewUndoTransaction
        | Some textUndoHistory ->
            let transaction = textUndoHistory.CreateTransaction(name)
            let editorOperations = _editorOperationsFactoryService.GetEditorOperations textView
            new TextViewUndoTransaction(name, Some transaction, Some editorOperations) :> ITextViewUndoTransaction

    /// Create a linked undo transaction.
    member x.CreateLinkedUndoTransaction (name : string) =
        VimTrace.TraceInfo("Open Linked Undo Transaction: {0}", name)

        // When there is a linked undo transaction active the top of the undo stack should always
        // be a Linked item
        if _linkedUndoTransactionStack.Count = 0 then
            _undoStack <- UndoRedoData.Linked 0 :: _undoStack

        let linkedUndoTransaction = new LinkedUndoTransaction(name, x) 
        _linkedUndoTransactionStack.Push(linkedUndoTransaction)
        linkedUndoTransaction :> ILinkedUndoTransaction

    /// Undo and redo are basically the same operation with the stacks passing data in the 
    /// opposite order.  This is the common behavior
    member x.UndoRedoCommon doUndoRedo sourceStack destStack count errorMessage =
        _inUndoRedo <- true
        try
            try
                let mutable sourceStack = sourceStack
                let mutable destStack = destStack
                for i = 1 to count do
                    match sourceStack with
                    | [] ->
                        // Happens when the user asks to undo / redo and there is nothing on the 
                        // stack.  The 'CanUndo' property is worthless here because it just returns
                        // 'true'.  Fall back to Visual Studio undo here.
                        doUndoRedo 1
                        destStack <- x.AddToStackNormal 1 destStack
                    | UndoRedoData.Linked count :: tail -> 
                        doUndoRedo count 
                        destStack <- UndoRedoData.Linked count :: destStack
                        sourceStack <- tail 
                    | UndoRedoData.Normal count :: tail ->
                        doUndoRedo 1
                        destStack <- x.AddToStackNormal 1 destStack
                        sourceStack <-
                            if count > 1 then
                                UndoRedoData.Normal (count - 1) :: tail
                            else
                                tail

                sourceStack, destStack
            with
            | _ ->
                _statusUtil.OnError errorMessage
                List.empty, List.empty
        finally
            _inUndoRedo <- false

    /// Do 'count' undo operations from the perspective of VsVim.  This is different than the actual
    /// number of undo operations we actually have to perform because of ILinkedUndoTransaction items
    member x.Undo count =
        x.CheckForBrokenUndoRedoChain()
        match _textUndoHistory with
        | None -> _statusUtil.OnError Resources.Internal_UndoRedoNotSupported
        | Some textUndoHistory ->
            let undoStack, redoStack = x.UndoRedoCommon (fun count -> textUndoHistory.Undo count) _undoStack _redoStack count Resources.Internal_CannotUndo
            _undoStack <- undoStack
            _redoStack <- redoStack

    /// Do 'count' redo transactions from the perspective of VsVim.  This is different than the actual
    /// number of redo operations we actually have to perform because of the ILinkedUndoTransaction items
    member x.Redo count =
        x.CheckForBrokenUndoRedoChain()
        match _textUndoHistory with
        | None -> _statusUtil.OnError Resources.Internal_UndoRedoNotSupported
        | Some textUndoHistory ->
            let redoStack, undoStack = x.UndoRedoCommon (fun count -> textUndoHistory.Redo count) _redoStack _undoStack count Resources.Internal_CannotRedo
            _undoStack <- undoStack
            _redoStack <- redoStack

    /// Called when a linked undo transaction is completed.  Must guard heavily against errors here in 
    /// particular
    ///
    ///     - transactions we orphaned in ResetState because of detected errors
    ///     - transactions closed out of order 
    member x.LinkedUndoTransactionClosed (linkedUndoTransaction : LinkedUndoTransaction) = 
        if _linkedUndoTransactionStack.Count > 0 then
            if obj.ReferenceEquals(_linkedUndoTransactionStack.Peek(), linkedUndoTransaction) then
                // This is the most recently open linked transaction which is what we are expecting
                _linkedUndoTransactionStack.Pop() |> ignore
            elif _linkedUndoTransactionStack.Contains(linkedUndoTransaction) then
                // This is a valid open transaction but it's not the top.  This means our state is corrupted
                // in some way and we need to reset it
                x.ResetState()
                VimTrace.TraceInfo("!!! Bad linked undo close order")
                _statusUtil.OnError Resources.Common_UndoChainOrderError

        // The other important case to handle here is orphaned transactions.  Basically 
        // instances which were alive when we called ResetState.  They are not a part of the 
        // current stack hence will never be processed above

    member x.EditWithUndoTransaction name textView action = 
        use undoTransaction = x.CreateTextViewUndoTransaction name textView
        undoTransaction.AddBeforeTextBufferChangePrimitive()
        let ret = action()
        undoTransaction.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()
        ret

    /// Called when an undo transaction completes and is added to the undo stack.  Need to record that
    /// here in order to get the correct undo behavior
    member x.OnUndoTransactionCompleted result =
        if _linkedUndoTransactionStack.Count = 0 then
            // Just a normal undo transaction.
            _undoStack <- x.AddToStackNormal 1 _undoStack
        else
            // Top of the stack should always be a Linked item.  If it's not don't throw as it will
            // permanently trash undo in the ITextBuffer. 
            let count, tail =
                match _undoStack with
                | [] -> 
                    1, []
                | head :: tail ->
                    match head with
                    | UndoRedoData.Normal _ -> 1, tail
                    | UndoRedoData.Linked count -> count + 1, tail
            _undoStack <- UndoRedoData.Linked count :: tail

        // Both types should empty the redo stack
        _redoStack <- List.Empty

    /// Called when an undo / redo operation occurs in the ITextBuffer.  If another piece of code
    /// is manipulating the ITextBuffer undo stack we need to update our stack accordingly.
    member x.OnUndoRedoHappened () = 
        if not _inUndoRedo then
            // Someone else is directly manipulating the undo / redo stack.  We've lost our ability 
            // to do anything here.  Reset our state now to prevent undo errors
            x.ResetState()
            VimTrace.TraceInfo("!!! Unexpected undo / redo")
            _statusUtil.OnError Resources.Common_UndoRedoUnexpected

    interface IUndoRedoOperations with
        member x.InLinkedUndoTransaction = x.InLinkedUndoTransaction
        member x.StatusUtil = _statusUtil
        member x.Close() = x.Close()
        member x.CreateUndoTransaction name = x.CreateUndoTransaction name
        member x.CreateTextViewUndoTransaction name textView = x.CreateTextViewUndoTransaction name textView
        member x.CreateLinkedUndoTransaction name = x.CreateLinkedUndoTransaction name
        member x.Redo count = x.Redo count
        member x.Undo count = x.Undo count
        member x.EditWithUndoTransaction name textView action = x.EditWithUndoTransaction name textView action

