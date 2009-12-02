#light

namespace VimCore.Modes.Command
open VimCore

type internal CommandMode =
    interface IMode
    new : IVimBufferData -> CommandMode
    
