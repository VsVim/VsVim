#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Classification
open System.Collections.Generic
open System.ComponentModel.Composition

type internal StatusUtil() = 
    let mutable _vimBuffer : VimBuffer option = None

    member x.VimBuffer 
        with get () = _vimBuffer
        and set value = _vimBuffer <- value

    member x.DoWithBuffer func = 
        match _vimBuffer with
        | None -> ()
        | Some buffer -> func buffer

    interface IStatusUtil with
        member x.OnStatus msg = x.DoWithBuffer (fun buffer -> buffer.RaiseStatusMessage msg)
        member x.OnError msg = x.DoWithBuffer (fun buffer -> buffer.RaiseErrorMessage msg)
        member x.OnWarning msg = x.DoWithBuffer (fun buffer -> buffer.RaiseWarningMessage msg)
        member x.OnStatusLong msgSeq = x.DoWithBuffer (fun buffer -> msgSeq |> StringUtil.combineWith System.Environment.NewLine |> buffer.RaiseStatusMessage)

type internal PropagatingStatusUtil() = 
    let _statusUtilList = List<IStatusUtil>()

    member x.StatusUtilList = _statusUtilList

    interface IStatusUtil with
        member x.OnStatus msg = _statusUtilList |> Seq.iter (fun x -> x.OnStatus msg)
        member x.OnError msg = _statusUtilList |> Seq.iter (fun x -> x.OnError msg)
        member x.OnWarning msg = _statusUtilList |> Seq.iter (fun x -> x.OnWarning msg)
        member x.OnStatusLong msg = _statusUtilList |> Seq.iter (fun x -> x.OnStatusLong msg)

[<Export(typeof<IStatusUtilFactory>)>]
[<Export(typeof<IVimBufferCreationListener>)>]
type StatusUtilFactory () = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = new System.Object()

    /// Get or create an PropagatingStatusUtil instance for the ITextBuffer
    member x.GetStatusUtilForBuffer (textBuffer : ITextBuffer) =
        textBuffer.Properties.GetOrCreateSingletonProperty(_key, (fun _ -> PropagatingStatusUtil()))

    /// Get or create an StatusUtil instance for the ITextView
    member x.GetStatusUtilForView (textView : ITextView) =
        textView.Properties.GetOrCreateSingletonProperty(_key, (fun _ -> StatusUtil()))

    /// When an IVimBuffer is created go ahead and update the backing VimBuffer value for
    /// the status util
    member x.VimBufferCreated (vimBuffer : IVimBuffer) = 
        try
            let vimBufferRaw = vimBuffer :?> VimBuffer

            let statusUtil = x.GetStatusUtilForView vimBuffer.TextView
            statusUtil.VimBuffer <- Some vimBufferRaw

            let propagatingStatusUtil = x.GetStatusUtilForBuffer vimBuffer.TextBuffer
            propagatingStatusUtil.StatusUtilList.Add(statusUtil)

            // The code above has created a link between both ITextBuffer -> ITextView 
            // and ITextView -> IVimBuffer.  Need to break all of these links when
            // IVimBuffer is closed to prevet a memory leak
            vimBuffer.Closed |> Observable.add (fun _ ->
                propagatingStatusUtil.StatusUtilList.Remove(statusUtil) |> ignore
                statusUtil.VimBuffer <- None)
        with
            | :? System.InvalidCastException -> ()

    interface IStatusUtilFactory with
        member x.EmptyStatusUtil = StatusUtil() :> IStatusUtil
        member x.GetStatusUtilForBuffer textBuffer = x.GetStatusUtilForBuffer textBuffer :> IStatusUtil
        member x.GetStatusUtilForView textView = x.GetStatusUtilForView textView :> IStatusUtil

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.VimBufferCreated buffer



