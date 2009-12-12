#light

namespace VimCore.Modes.Normal
open VimCore

module internal Operations =

    val Mark : NormalModeData -> NormalModeResult
    val JumpToMark : NormalModeData -> NormalModeResult
    val CharGCommand : NormalModeData -> NormalModeResult
    val InsertLineAbove : NormalModeData -> NormalModeResult


