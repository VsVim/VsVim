#light

namespace Vim.Modes.Normal
open Vim

module internal Operations =

    val Mark : NormalModeData -> NormalModeResult
    val JumpToMark : NormalModeData -> NormalModeResult
    val CharGCommand : NormalModeData -> NormalModeResult
    val InsertLineAbove : NormalModeData -> NormalModeResult
    val ReplaceChar : NormalModeData -> NormalModeResult
    val YankLines : NormalModeData -> NormalModeResult
    val DeleteCharacterAtCursor : NormalModeData -> NormalModeResult 
    val DeleteCharacterBeforeCursor : NormalModeData -> NormalModeResult


