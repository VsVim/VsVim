
#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Vim.Interpreter

/// Bulk operations include repeat and macro commands.  This inteface is used to notify the 
/// system that a bulk operation is begining / ending
type internal IBulkOperations =

    /// True when in the middle of a bulk operation
    abstract InBulkOperation : bool

    /// Called at the start of a bulk operation.  The disposing of the returned IDisposable
    /// instance represents the conclusion of the bulk operation
    abstract BeginBulkOperation : unit -> System.IDisposable

type internal IVimBufferFactory =

    /// Create an IVimTextBuffer for the given ITextView
    abstract CreateVimTextBuffer : textBuffer : ITextBuffer -> vim : IVim -> IVimTextBuffer

    /// Create a VimBufferData value for the given values
    abstract CreateVimBufferData : vimTextBuffer : IVimTextBuffer -> textView : ITextView -> IVimBufferData

    /// Create an IVimBuffer for the given parameters
    abstract CreateVimBuffer : vimBufferData : IVimBufferData -> IVimBuffer

type internal IVimInterpreterFactory = 

    /// Create a IVimInterpreter for the given IVimBuffer.  
    abstract CreateVimInterpreter : vimBuffer : IVimBuffer -> fileSystem : IFileSystem -> IVimInterpreter

