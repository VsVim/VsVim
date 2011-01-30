#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal ChangeTracker
    (   _textChangeTrackerFactory : ITextChangeTrackerFactory ) =

    /// This is a prime example of the flaw in this architecture.  The last 
    /// change should be independent of the IVimBuffer which caused it.  Need
    /// to redesign commands to be declarative (Discriminated Union) and store
    /// that so it can be applied to any IVimBuffer.  Currently tracked
    /// by issue #363
    let mutable _last : (IVimBuffer * RepeatableChange) option = None
    
    member private x.OnVimBufferCreated (buffer:IVimBuffer) =
        let handler = x.OnCommandRan buffer
        buffer.NormalMode.CommandRunner.CommandRan |> Event.add handler
        buffer.VisualLineMode.CommandRunner.CommandRan |> Event.add handler
        buffer.VisualBlockMode.CommandRunner.CommandRan |> Event.add handler
        buffer.VisualCharacterMode.CommandRunner.CommandRan |> Event.add handler

        // Need to watch for an IVimBuffer close to avoid leaks.  Else we could be
        // holding onto the last chnaged IVimBuffer long after it's closed.  
        let clearOnClosed() = 
            match _last with 
            | Some(lastBuffer, _) -> 
                if lastBuffer = buffer then
                    _last <- None
            | None -> 
                ()
        buffer.Closed |> Event.add (fun _ -> clearOnClosed())

        let tracker = _textChangeTrackerFactory.GetTextChangeTracker buffer
        tracker.ChangeCompleted |> Event.add (x.OnTextChanged buffer)

    member private x.OnCommandRan buffer ((data:CommandRunData),_) = 
        let command = data.Command
        if command.IsMovement || command.IsSpecial then
            // Movement and special commands don't participate in change tracking
            ()
        elif command.IsRepeatable then 
            let change = RepeatableChange.CommandChange data
            _last <- Some (buffer, change)
        else 
            _last <- None

    member private x.OnTextChanged buffer data = 
        let useCurrent() = _last <- (buffer, RepeatableChange.TextChange(data)) |> Some
        match _last with
        | None -> 
            _last <- (buffer, RepeatableChange.TextChange(data)) |> Some
        | Some(_, last) ->
            match last with
            | RepeatableChange.LinkedChange (_) -> useCurrent()
            | RepeatableChange.TextChange (_) -> useCurrent()
            | RepeatableChange.CommandChange (change) -> 
                if Util.IsFlagSet change.Command.CommandFlags CommandFlags.LinkedWithNextTextChange then
                    let change = RepeatableChange.LinkedChange (RepeatableChange.CommandChange change,RepeatableChange.TextChange data) 
                    _last <- Some (buffer, change)
                else 
                    useCurrent()
            
    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.OnVimBufferCreated buffer

    interface IChangeTracker with
        member x.LastChange = 
            match _last with
            | Some (_, change) -> Some change
            | None -> None
