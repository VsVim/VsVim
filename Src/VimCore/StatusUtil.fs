#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Collections.Generic
open System.ComponentModel.Composition

type internal StatusUtil() = 
    let mutable _vimBuffer: IVimBufferInternal option = None

    member x.VimBuffer 
        with get () = _vimBuffer
        and set value = _vimBuffer <- value

    member x.DoWithBuffer (label: string) func (msg: string) = 
        VimTrace.TraceError("{0} Start{1}{2}", label, System.Environment.NewLine, msg)
        VimTrace.TraceError("{1} End", label)
        match _vimBuffer with
        | None -> ()
        | Some buffer -> msg |> func buffer

    interface IStatusUtil with
        member x.OnStatus msg = msg |> x.DoWithBuffer "Status" (fun buffer -> buffer.RaiseStatusMessage)
        member x.OnError msg = msg |> x.DoWithBuffer "Error" (fun buffer -> buffer.RaiseErrorMessage)
        member x.OnWarning msg = msg |> x.DoWithBuffer "Warning" (fun buffer -> buffer.RaiseWarningMessage)
        member x.OnStatusLong msgSeq =
            msgSeq
            |> StringUtil.CombineWith System.Environment.NewLine
            |> x.DoWithBuffer "StatusLong" (fun buffer -> buffer.RaiseStatusMessage)

type internal PropagatingStatusUtil() = 
    let _statusUtilList = List<IStatusUtil>()

    member x.StatusUtilList = _statusUtilList

    interface IStatusUtil with
        member x.OnStatus msg = _statusUtilList |> Seq.iter (fun x -> x.OnStatus msg)
        member x.OnError msg = _statusUtilList |> Seq.iter (fun x -> x.OnError msg)
        member x.OnWarning msg = _statusUtilList |> Seq.iter (fun x -> x.OnWarning msg)
        member x.OnStatusLong msg = _statusUtilList |> Seq.iter (fun x -> x.OnStatusLong msg)

[<Export(typeof<IStatusUtilFactory>)>]
type StatusUtilFactory () = 

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _key = new System.Object()

    /// Get or create an PropagatingStatusUtil instance for the ITextBuffer
    member x.GetStatusUtilForBuffer (textBuffer: ITextBuffer) =
        textBuffer.Properties.GetOrCreateSingletonProperty(_key, (fun unused -> PropagatingStatusUtil()))

    /// Get or create an StatusUtil instance for the ITextView
    member x.GetStatusUtilForView (textView: ITextView) =
        textView.Properties.GetOrCreateSingletonProperty(_key, (fun unused -> StatusUtil()))

    /// When an IVimBuffer is created go ahead and update the backing VimBuffer value for
    /// the status util
    member x.InitializeVimBuffer (vimBuffer: IVimBufferInternal) = 
        try
            let statusUtil = x.GetStatusUtilForView vimBuffer.TextView
            statusUtil.VimBuffer <- Some vimBuffer

            let propagatingStatusUtil = x.GetStatusUtilForBuffer vimBuffer.TextView.TextBuffer
            propagatingStatusUtil.StatusUtilList.Add(statusUtil)

            // The code above has created a link between both ITextBuffer -> ITextView 
            // and ITextView -> IVimBuffer.  Need to break all of these links when
            // IVimBuffer is closed to prevet a memory leak
            vimBuffer.TextView.Closed |> Observable.add (fun _ ->
                propagatingStatusUtil.StatusUtilList.Remove(statusUtil) |> ignore
                statusUtil.VimBuffer <- None)
        with
            | :? System.InvalidCastException -> ()

    interface IStatusUtilFactory with
        member x.EmptyStatusUtil = StatusUtil() :> IStatusUtil
        member x.GetStatusUtilForBuffer textBuffer = x.GetStatusUtilForBuffer textBuffer :> IStatusUtil
        member x.GetStatusUtilForView textView = x.GetStatusUtilForView textView :> IStatusUtil
        member x.InitializeVimBuffer vimBuffer = x.InitializeVimBuffer vimBuffer



