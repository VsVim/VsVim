#light

namespace DefaultVimHost
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition

[<Export(typeof<IWpfTextViewCreationListener>)>]
type Factory =
    interface IWpfTextViewCreationListener with 
        member x.TextViewCreated(textView:IWpfTextView) =
            ()
