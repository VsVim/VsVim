
namespace Vim
open Vim.Interpreter

type internal KeyMap =

    interface IKeyMap

    new : settings : IVimGlobalSettings * variableMap : VariableMap -> KeyMap 

