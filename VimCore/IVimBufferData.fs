
#light

namespace VimCore

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Represents the core pieces of data available for an IVimBuffer implementation
type IVimBufferData =
    abstract TextView : IWpfTextView
    abstract TextBuffer : ITextBuffer
    abstract TextSnapshot : ITextSnapshot
    abstract VimHost : IVimHost
    abstract Settings : VimSettings
    abstract RegisterMap : IRegisterMap
