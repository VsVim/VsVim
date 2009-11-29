
#light

namespace VimCore

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Represents the core pieces of data available for an IVimBuffer implementation
type IVimBufferData =
    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// View of the filek
    abstract TextView : IWpfTextView

    /// Underyling ITextBuffer Vim is operating under
    abstract TextBuffer : ITextBuffer
    abstract TextSnapshot : ITextSnapshot
    abstract VimHost : IVimHost
    abstract Settings : VimSettings
    abstract RegisterMap : IRegisterMap
