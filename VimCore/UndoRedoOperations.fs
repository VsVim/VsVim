#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations


type UndoTransaction 
    (
        _transaction : ITextUndoTransaction option,
        _editorOperations : IEditorOperations option
    ) =

    interface IUndoTransaction with
        member x.AddAfterTextBufferChangePrimitive() =
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddAfterTextBufferChangePrimitive()
        member x.AddBeforeTextBufferChangePrimitive() = 
            match _editorOperations with
            | None -> ()
            | Some editorOperations -> editorOperations.AddBeforeTextBufferChangePrimitive()
        member x.Complete () = 
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Complete()
        member x.Cancel() = 
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Cancel()
        member x.Dispose() = 
            match _transaction with
            | None -> ()
            | Some transaction -> transaction.Dispose()

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

type LinkedUndoTransaction
    (
        _undoRedoOperations : UndoRedoOperations
    ) = 

    member x.Complete () = _undoRedoOperations.LinkedTransactionClosed()

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
        _history : ITextUndoHistory option,
        _editorOperations : IEditorOperations 
    ) as this =

    let mutable _openLinkedTransactionCount = 0
    let mutable _inUndoRedo = false
    let mutable _undoStack : UndoRedoData list = List.empty
    let mutable _redoStack : UndoRedoData list = List.empty
    let _bag = DisposableBag()

    do
        match _history with 
        | None ->
            ()
        | Some history -> 
            history.UndoTransactionCompleted
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

            history.UndoRedoHappened
            |> Observable.subscribe (fun _ -> this.OnUndoRedoHappened())
            |> _bag.Add

    /// Closing simply means we need to detach from our event handlers so memory can
    /// be reclaimed.  IVimBuffer operates at an ITextView level but here we are 
    /// handling events at an ITextBuffer level.  This easily creates a leak if we 
    /// remain attached and only the ITextView is closed.  
    member x.Close () = _bag.DisposeAll()

    member x.UndoStack = _undoStack
    member x.RedoStack = _redoStack

    /// Add 'count' normal undo transactions to the top of the stack
    member x.AddToStackNormal count list =
        match list with
        | [] ->
            [ UndoRedoData.Normal count ]
        | head :: tail ->
            let head, tail = 
                match head with
                | UndoRedoData.Normal c -> UndoRedoData.Normal (count + c), tail
                | UndoRedoData.Linked _ -> UndoRedoData.Normal count, list
            head :: tail 

    member x.CreateUndoTransaction name = 
        match _history with
        | None -> 
            // Don't support either if the history is not present.  While we have an instance
            // of IEditorOperations it will still fail because internally it's trying to access
            // the same ITextUndorHistory which is null
            new UndoTransaction(None, None) :> IUndoTransaction
        | Some(history) ->
            let transaction = history.CreateTransaction(name)
            new UndoTransaction(Some transaction, Some _editorOperations) :> IUndoTransaction

    /// Create a linked undo transaction.
    member x.CreateLinkedUndoTransaction() =

        // When there is a linked undo transaction active the top of the undo stack should always
        // be a Linked item
        _openLinkedTransactionCount <- _openLinkedTransactionCount + 1
        if _openLinkedTransactionCount = 1 then
            _undoStack <- UndoRedoData.Linked 0 :: _undoStack

        new LinkedUndoTransaction(x) :> ILinkedUndoTransaction

    /// Undo and redo are basically the same operation with the stacks passing data in the 
    /// opposite order.  This is the common behavior
    member x.UndoRedoCommon doUndoRedo sourceStack destStack count errorMessage =
        try
            /// Do a single undo from the perspective of Vim 
            let doOne sourceStack destStack =
                let realCount, newSourceStack, newDestTop = 
                    match sourceStack with
                    | [] ->
                        // Happens when the user asks to undo / redo and there is nothing on the 
                        // stack.  The 'CanUndo' property is worthless here because it just returns
                        // 'true'.  Let the undo fail and alert the user
                        1, [], None
                    | head :: tail ->
                        match head with
                        | UndoRedoData.Normal count ->
                            if count <= 1 then
                                1, tail, None
                            else
                                1, UndoRedoData.Normal (count - 1) :: tail, None
                        | UndoRedoData.Linked count ->
                            count, tail, Some head

                _inUndoRedo <- true
                try
                    // Undo the real count of operations to get a Vim undo behavior then 
                    // update the undo stack
                    doUndoRedo realCount

                    // Rebuild the destination stack
                    let newDestStack = 
                        match newDestTop with
                        | None -> x.AddToStackNormal 1 destStack
                        | Some linked -> linked :: destStack

                    newSourceStack, newDestStack
                finally
                    _inUndoRedo <- false

            // Do the undo / redo 'count' times
            let rec doCount sourceStack destStack count = 
                let sourceStack, destStack = doOne sourceStack destStack
                if count = 1 then sourceStack, destStack
                else doCount sourceStack destStack (count - 1)

            doCount sourceStack destStack count
        with
            | :? System.NotSupportedException -> 
                _statusUtil.OnError errorMessage
                List.empty, List.empty

    /// Do 'count' undo operations from the perspective of VsVim.  This is different than the actual
    /// number of undo operations we actually have to perform because of ILinkedUndoTransaction items
    member x.Undo count =
        match _history with
        | None -> 
            _statusUtil.OnError Resources.Internal_UndoRedoNotSupported
        | Some history ->
            let undoStack, redoStack = x.UndoRedoCommon (fun count -> history.Undo count) _undoStack _redoStack count Resources.Internal_CannotUndo
            _undoStack <- undoStack
            _redoStack <- redoStack

    /// Do 'count' redo transactions from the perspective of VsVim.  This is different than the actual
    /// number of redo operations we actually have to perform because of the ILinkedUndoTransaction items
    member x.Redo count =
        match _history with
        | None -> _statusUtil.OnError Resources.Internal_UndoRedoNotSupported
        | Some(history) ->
            let redoStack, undoStack = x.UndoRedoCommon (fun count -> history.Redo count) _redoStack _undoStack count Resources.Internal_CannotRedo
            _undoStack <- undoStack
            _redoStack <- redoStack

    member x.LinkedTransactionClosed() =
        _openLinkedTransactionCount <- _openLinkedTransactionCount - 1

    member x.EditWithUndoTransaction name action = 
        use undoTransaction = x.CreateUndoTransaction name
        undoTransaction.AddBeforeTextBufferChangePrimitive()
        let ret = action()
        undoTransaction.AddAfterTextBufferChangePrimitive()
        undoTransaction.Complete()
        ret

    /// Called when an undo transaction completes and is added to the undo stack.  Need to record that
    /// here in order to get the correct undo behavior
    member x.OnUndoTransactionCompleted result =

        if _openLinkedTransactionCount = 0 then
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

        // If we're the ones driving the undo / redo then no update is needed.
        if not _inUndoRedo then

            // For now we just keep it simple and collapse the stacks to all normal transactions.  If
            // we find a real scenario around this later on then we'll do smarter logic.  The code
            // already explicitly handles incorrect stacks so just empty them
            _undoStack <- List.empty
            _redoStack <- List.empty

    interface IUndoRedoOperations with
        member x.StatusUtil = _statusUtil
        member x.Close() = x.Close()
        member x.CreateUndoTransaction name = x.CreateUndoTransaction name
        member x.CreateLinkedUndoTransaction () = x.CreateLinkedUndoTransaction()
        member x.Redo count = x.Redo count
        member x.Undo count = x.Undo count
        member x.EditWithUndoTransaction name action = x.EditWithUndoTransaction name action

