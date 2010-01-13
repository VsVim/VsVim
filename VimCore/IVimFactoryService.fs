#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging

/// MEF component which can spin up Vi components
type IVimFactoryService =
    abstract CreateVim : IVimHost -> IVim
    abstract CreateVimBuffer : IVimHost -> IWpfTextView -> bufferName:string -> IVimBuffer
    abstract CreateKeyProcessor : IVimBuffer -> KeyProcessor
    abstract CreateMouseProcessor : IVimBuffer -> IMouseProcessor
    abstract CreateTagger : IVimBuffer -> ITagger<TextMarkerTag>
