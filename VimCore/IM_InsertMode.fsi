#light

namespace VimCore.Modes.Insert
open VimCore

type internal InsertMode =
    interface IMode
    new : IVimBufferData -> InsertMode
