#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open System.ComponentModel.Composition

type internal StatusUtil() = 
    let mutable _buffer : VimBuffer option = None

    member x.VimBuffer 
        with get () = _buffer
        and set value = _buffer <- value

    member x.DoWithBuffer func = 
        match _buffer with
        | None -> ()
        | Some(buffer) -> func buffer

    interface IStatusUtil with
        member x.OnStatus msg = x.DoWithBuffer (fun buffer -> buffer.RaiseStatusMessage msg)
        member x.OnError msg = x.DoWithBuffer (fun buffer -> buffer.RaiseErrorMessage msg)
        member x.OnWarning msg = x.DoWithBuffer (fun buffer -> buffer.RaiseWarningMessage msg)
        member x.OnStatusLong msgSeq = x.DoWithBuffer (fun buffer -> buffer.RaiseStatusMessageLong msgSeq)

[<Export(typeof<IStatusUtilFactory>)>]
[<Export(typeof<IVimBufferCreationListener>)>]
type StatusUtilFactory () = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = new System.Object()

    /// Get or create an StatusUtil instance for the ITextView 
    member x.GetOrCreateStatusUtil (textView : ITextView) =
        textView.Properties.GetOrCreateSingletonProperty(_key, (fun _ -> StatusUtil()))

    /// When an IVimBuffer is created go ahead and update the backing VimBuffer value for
    /// the status util
    member x.VimBufferCreated (buffer : IVimBuffer) = 
        let textView = buffer.TextView
        try
            let bufferRaw = buffer :?> VimBuffer
            let statusUtil = x.GetOrCreateStatusUtil buffer.TextView
            statusUtil.VimBuffer <- Some bufferRaw
            buffer.Closed |> Observable.add (fun _ ->
                textView.Properties.RemoveProperty(_key) |> ignore
                statusUtil.VimBuffer <- None)
        with
            | :? System.InvalidCastException -> ()

    interface IStatusUtilFactory with
        member x.GetStatusUtil textView = x.GetOrCreateStatusUtil textView :> IStatusUtil

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.VimBufferCreated buffer



