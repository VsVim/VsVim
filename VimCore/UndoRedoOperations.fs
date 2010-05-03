#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type UndoTransaction (_transaction : ITextUndoTransaction option ) =

    interface IUndoTransaction with
        member x.Complete () = 
            match _transaction with
            | None -> ()
            | Some(transaction) -> transaction.Complete()
        member x.Cancel() = 
            match _transaction with
            | None -> ()
            | Some(transaction) -> transaction.Cancel()
        member x.Dispose() = 
            match _transaction with
            | None -> ()
            | Some(transaction) -> transaction.Dispose()

type UndoRedoOperations 
    (
        _statusUtil : IStatusUtil,
        _history : ITextUndoHistory option) =

    member x.CreateUndoTransaction name = 
        match _history with
        | None -> new UndoTransaction(None) :> IUndoTransaction
        | Some(history) ->
            let transaction = history.CreateTransaction(name)
            new UndoTransaction(Some transaction) :> IUndoTransaction

    member x.Undo count =
        match _history with
        | None -> _statusUtil.OnError Resources.UndoRedo_NotSupported
        | Some(history) ->
            try
                if history.CanUndo then
                    history.Undo(count)
            with
                | :? System.NotSupportedException -> _statusUtil.OnError Resources.UndoRedo_CannotUndo

    member x.Redo count =
        match _history with
        | None -> _statusUtil.OnError Resources.UndoRedo_NotSupported
        | Some(history) ->
            try 
                if history.CanRedo then
                    history.Redo(count)
            with
                | :? System.NotSupportedException -> _statusUtil.OnError Resources.UndoRedo_CannotRedo

    interface IUndoRedoOperations with
        member x.StatusUtil = _statusUtil
        member x.CreateUndoTransaction name = x.CreateUndoTransaction name
        member x.Redo count = x.Redo count
        member x.Undo count = x.Undo count

