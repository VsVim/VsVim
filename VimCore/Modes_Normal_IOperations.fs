#light

namespace Vim.Modes.Normal
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type internal IOperations =
    abstract Mark : NormalModeData -> NormalModeResult
    abstract JumpToMark : NormalModeData -> NormalModeResult
    abstract CharGCommand : NormalModeData -> NormalModeResult
    abstract InsertLineAbove : NormalModeData -> NormalModeResult
    abstract ReplaceChar : NormalModeData -> NormalModeResult
    abstract YankLines : NormalModeData -> NormalModeResult
    abstract DeleteCharacterAtCursor : NormalModeData -> NormalModeResult 
    abstract DeleteCharacterBeforeCursor : NormalModeData -> NormalModeResult


