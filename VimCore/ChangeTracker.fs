#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal ChangeTracker() =

    let mutable _last : RepeatableChange option = None
    
    /// Tracks the current active text change.  This will grow as the user edits
    let mutable _currentTextChange : (ITextBuffer * Span) option = None
    
    member private x.OnVimBufferCreated (buffer:IVimBuffer) =
        buffer.NormalMode.CommandRunner.CommandRan |> Event.add x.OnCommandRan
        buffer.VisualLineMode.CommandRunner.CommandRan |> Event.add x.OnCommandRan
        buffer.VisualBlockMode.CommandRunner.CommandRan |> Event.add x.OnCommandRan
        buffer.VisualCharacterMode.CommandRunner.CommandRan |> Event.add x.OnCommandRan
        buffer.SwitchedMode |> Event.add (fun _ -> _currentTextChange <- None)

        // Listen to the ITextBuffer.Change event.  It's important to unsubscribe to this as the ITextBuffer
        // can live much longer than an IVimBuffer instance
        let handler = buffer.TextBuffer.Changed |> Observable.subscribe (fun args -> x.OnTextChanged buffer args)
        buffer.TextView.Closed |> Event.add (fun _ -> handler.Dispose())

    member private x.OnCommandRan ((data:CommandRunData),_) = 
        let command = data.Command
        if command.IsMovement || command.IsSpecial then
            // Movement and special commandsd don't participate in change tracking
            ()
        elif command.IsRepeatable then _last <- CommandChange data |> Some
        else _last <- None

    member private x.OnTextChanged (buffer:IVimBuffer) args =

        // Ignore changes which do not happen on focused windows.  Doing otherwise allows
        // random tools to break this feature
        if buffer.TextView.HasAggregateFocus then

            // Also at this time we only support contiguous changes (or rather a single change)
            if args.Changes.Count = 1 then
                let change = args.Changes.Item(0)
    
                let useCurrentChange () = 
                    _currentTextChange <- (buffer.TextBuffer,change.NewSpan) |> Some
                    _last <- TextChange(change.NewText) |> Some
    
                match _currentTextChange with
                | None -> useCurrentChange()
                | Some(oldBuffer,span) -> 
    
                    // Make sure this is a contiguous change that is not a delete
                    if span.End = change.OldPosition && change.OldLength = 0 && oldBuffer = buffer.TextBuffer then
                        let span = Span(span.Start, span.Length + change.NewLength)
                        _currentTextChange <- (buffer.TextBuffer,span) |> Some
                        _last <- TextChange(buffer.TextBuffer.CurrentSnapshot.GetText(span)) |> Some
                    else 
                        useCurrentChange()
            else 
                _last <- None
                _currentTextChange <- None
            

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.OnVimBufferCreated buffer

    interface IChangeTracker with
        member x.LastChange = _last