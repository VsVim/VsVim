#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Collections.Generic

/// Information about how many actual undo / redo operations need
/// to be performed to do a single Vim undo 
[<RequireQualifiedAccess>]
type UndoRedoData =

    /// The stack contains 'count' normal undo / redo transactions which line up
    /// with Vim behavior.  The count is kept here instead of having a stack of 'count'
    /// depth in order to keep memory allocations minimal.
    | Normal of int

    /// The stack contains 'count' normal undo / redo transactions which line up 
    /// with a single Vim undo / redo transaction.  
    ///
    /// The bool is true when this is a closed linked transaction.  It has been fully completed
    /// and is no longer being built by closing transactions
    | Linked of int * bool

[<RequireQualifiedAccess>]
type TransactionCloseResult =
    /// The close was expected and has been removed from the stack
    | Expected

    /// The transaction was closed out of order 
    | BadOrder
    
    /// This was a previously orphaned transaction.  Essentially one which was open when we
    /// called ResetState.  
    | Orphaned

type NormalUndoTransaction 
    (
        _name : string,
        _transaction : ITextUndoTransaction option,
        _undoRedoOperations : UndoRedoOperations
    ) =

    let mutable _isComplete = false

    member x.CompleteCore() = 
        if not _isComplete then
            _isComplete <- true
            _undoRedoOperations.NormalUndoTransactionClosed x

    interface IUndoTransaction with
        member x.Complete () = 
            x.CompleteCore()
            VimTrace.TraceInfo("Complete Undo Transaction: {0}", _name)
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Complete()
        member x.Cancel() = 
            x.CompleteCore()
            VimTrace.TraceInfo("Cancel Undo Transaction: {0}", _name)
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Cancel()
        member x.Dispose() = 
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Dispose()

and TextViewUndoTransaction 
    (
        _name : string,
        _transaction : ITextUndoTransaction option,
        _editorOperations : IEditorOperations option,
        _undoRedoOperations : UndoRedoOperations
    ) =
    inherit NormalUndoTransaction(_name, _transaction, _undoRedoOperations)

    interface ITextViewUndoTransaction with
        member x.AddAfterTextBufferChangePrimitive() =
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddAfterTextBufferChangePrimitive()
        member x.AddBeforeTextBufferChangePrimitive() = 
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddBeforeTextBufferChangePrimitive()


/// This is a method for linking together several simple undo transactions into a
/// single undo unit.  Undo or Redo of this item will undo / redo every undo unit
/// that it is linked to
and LinkedUndoTransaction
    (
        _name : string,
        _flags : LinkedUndoTransactionFlags,
        _undoRedoOperations : UndoRedoOperations
    ) = 

    let mutable _isComplete = false

    member x.Flags = _flags

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

    let _linkedUndoTransactionStack = Stack<LinkedUndoTransaction>()
    let _normalUndoTransactionStack = Stack<NormalUndoTransaction>()
    let mutable _inUndoRedo = false

    // Contains the active set of operations to undo from the perspective of Vim.  If 
    // there is no history this should always be empty
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

    /// Are we in the middle of a normal undo transaction
    member x.InNormalUndoTransaction = _normalUndoTransactionStack.Count > 0

    /// Are we in the middle of a linked undo transaction
    member x.InLinkedUndoTransaction = _linkedUndoTransactionStack.Count > 0

    member x.LinkedUndoTransactionStack = _linkedUndoTransactionStack

    member x.NormalUndoTransactionStack = _normalUndoTransactionStack

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
                | UndoRedoData.Linked (0, _) -> UndoRedoData.Normal count, tail
                | UndoRedoData.Linked _ -> UndoRedoData.Normal count, list
            head :: tail 

    member x.ResetUndoRedoStacks() = 
        _undoStack <- List.empty
        _redoStack <- List.empty

    /// This method is called when the undo / redo management detects we have gotten into
    /// a bad state.  Essentially we've lost our ability to sync with the editor undo
    /// stack.  Reset all state at this point so that undo / redo goes back to the normal
    /// editor functions.  Edits after this point will sync back up with us
    member x.ResetState() =
        _normalUndoTransactionStack.Clear()
        _linkedUndoTransactionStack.Clear()
        x.ResetUndoRedoStacks()

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
            _statusUtil.OnError Resources.Undo_ChainBroken

    member x.CreateUndoTransaction (name : string) = 
        VimTrace.TraceInfo("Open Undo Transaction: {0}", name)
        let undoTransaction = 
            match _textUndoHistory with
            | None -> 
                new NormalUndoTransaction(name, None, x) 
            | Some textUndoHistory ->
                let transaction = textUndoHistory.CreateTransaction(name)
                new NormalUndoTransaction(name, Some transaction, x) 

        _normalUndoTransactionStack.Push(undoTransaction)
        undoTransaction :> IUndoTransaction

    member x.CreateTextViewUndoTransaction (name : string) (textView : ITextView) = 
        VimTrace.TraceInfo("Open Text View Undo Transaction: {0}", name)
        let textViewUndoTransaction = 
            match _textUndoHistory with
            | None -> 
                // Don't support either if the textUndoHistory is not present.  While we have an instance
                // of IEditorOperations it will still fail because internally it's trying to access
                // the same ITextUndorHistory which is null
                new TextViewUndoTransaction(name, None, None, x)
            | Some textUndoHistory ->
                let transaction = textUndoHistory.CreateTransaction(name)
                let editorOperations = _editorOperationsFactoryService.GetEditorOperations textView
                new TextViewUndoTransaction(name, Some transaction, Some editorOperations, x)

        _normalUndoTransactionStack.Push(textViewUndoTransaction)
        textViewUndoTransaction :> ITextViewUndoTransaction

    /// Create a linked undo transaction.
    member x.CreateLinkedUndoTransaction (name : string) flags =
        VimTrace.TraceInfo("Open Linked Undo Transaction: {0}", name)

        // A linked undo transaction works by simply counting all of the normal undo transactions
        // which are completed while it is open.  A normal *nested* editor undo transaction never
        // actually raises any events.  Only an outer one ever does.  Hence if an outer transaction
        // is already open here a linked transaction is pointless.  It will never receive any 
        // events.  
        //
        // It's really tempting to think that instead of relying on editor events from ITextUndoHistory 
        // we could just hook into the creation of normal transactions.  That doesn't work because Vim
        // is not the only source of undo events.  The editor frequently creates them hence there is no
        // real way to track the number of events properly.  This is just a broken scenario 
        if _normalUndoTransactionStack.Count > 0 then
            _statusUtil.OnError Resources.Undo_LinkedOpenError

        let linkedUndoTransaction = new LinkedUndoTransaction(name, flags, x) 
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
                    | UndoRedoData.Linked (count, completed) :: tail -> 
                        doUndoRedo count 
                        destStack <- UndoRedoData.Linked (count, completed) :: destStack
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

    /// Called when a transaction is closed.  Need to determine the close state based on the current
    /// expected undo stack 
    member x.UndoTransactionClosedCore<'T> (undoTransaction : 'T) (stack : Stack<'T>) : TransactionCloseResult =
        if stack.Count > 0 then
            if obj.ReferenceEquals(stack.Peek(), undoTransaction) then
                // This is the most recently open linked transaction which is what we are expecting
                stack.Pop() |> ignore
                TransactionCloseResult.Expected
            elif stack.Contains(undoTransaction) then
                TransactionCloseResult.BadOrder
            else
                TransactionCloseResult.Orphaned
        else
            TransactionCloseResult.Orphaned

    /// Called when a linked undo transaction is completed.  Must guard heavily against errors here in 
    /// particular
    ///
    ///     - transactions we orphaned in ResetState because of detected errors
    ///     - transactions closed out of order 
    member x.LinkedUndoTransactionClosed (linkedUndoTransaction : LinkedUndoTransaction) = 
        match x.UndoTransactionClosedCore linkedUndoTransaction _linkedUndoTransactionStack with
        | TransactionCloseResult.Expected -> 
            if _linkedUndoTransactionStack.Count = 0 && Option.isSome _textUndoHistory then
                let mutable hasData = false

                // The linked undo transaction is now done, need to freeze the data on the undo
                // stack 
                match _undoStack with
                | UndoRedoData.Linked (count, false) :: tail -> 
                    Contract.Assert(count > 0)
                    hasData <- true
                    _undoStack <- UndoRedoData.Linked (count, true) :: tail
                | _ -> ()

                // If a linked undo operation completes that contains 0 undo / redo items that very likely 
                // indicates a bug.  It can mean that VsVim has been unhooked from the undo / redo event
                // queue as described in #1387.  Notify the user and reset our state 
                if not hasData && not (Util.IsFlagSet linkedUndoTransaction.Flags LinkedUndoTransactionFlags.CanBeEmpty) then
                    x.ResetState()
                    VimTrace.TraceInfo("!!! Empty linked undo chain")
                    _statusUtil.OnError Resources.Undo_LinkedChainBroken
        
        | TransactionCloseResult.Orphaned -> ()
        | TransactionCloseResult.BadOrder -> 
            // This is a valid open transaction but it's not the top.  This means our state is corrupted
            // in some way and we need to reset it
            x.ResetState()
            VimTrace.TraceInfo("!!! Bad linked undo close order")
            _statusUtil.OnError Resources.Undo_ChainOrderErrorLinked

    /// Called when a normal undo transaction is completed.  Must guard heavily against errors here in 
    /// particular
    ///
    ///     - transactions we orphaned in ResetState because of detected errors
    ///     - transactions closed out of order 
    member x.NormalUndoTransactionClosed (normalUndoTransaction : NormalUndoTransaction) = 
        match x.UndoTransactionClosedCore normalUndoTransaction _normalUndoTransactionStack with
        | TransactionCloseResult.Expected -> ()
        | TransactionCloseResult.Orphaned -> ()
        | TransactionCloseResult.BadOrder -> 
            // This is a valid open transaction but it's not the top.  This means our state is corrupted
            // in some way and we need to reset it
            x.ResetState()
            VimTrace.TraceInfo("!!! Bad linked undo close order")
            _statusUtil.OnError Resources.Undo_ChainOrderErrorNormal

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
            // The first transaction which completes will create the linked item.  Otherwise it should 
            // just be adding to the one which is already there 
            let count, tail =
                match _undoStack with
                | [] -> 1, []
                | head :: tail ->
                    match head with
                    | UndoRedoData.Normal _ -> 1, _undoStack
                    | UndoRedoData.Linked (_, true) -> 1, _undoStack
                    | UndoRedoData.Linked (count, false) -> count + 1, tail
            _undoStack <- UndoRedoData.Linked (count, false) :: tail

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
            _statusUtil.OnError Resources.Undo_RedoUnexpected

    interface IUndoRedoOperations with
        member x.InLinkedUndoTransaction = x.InLinkedUndoTransaction
        member x.StatusUtil = _statusUtil
        member x.Close() = x.Close()
        member x.CreateUndoTransaction name = x.CreateUndoTransaction name
        member x.CreateTextViewUndoTransaction name textView = x.CreateTextViewUndoTransaction name textView
        member x.CreateLinkedUndoTransaction name = x.CreateLinkedUndoTransaction name LinkedUndoTransactionFlags.None
        member x.CreateLinkedUndoTransactionWithFlags name flags = x.CreateLinkedUndoTransaction name flags
        member x.Redo count = x.Redo count
        member x.Undo count = x.Undo count
        member x.EditWithUndoTransaction name textView action = x.EditWithUndoTransaction name textView action

