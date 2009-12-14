#light

namespace Vim.Modes.Insert
open Vim

type internal InsertMode =
    interface IMode
    new : IVimBufferData -> InsertMode
