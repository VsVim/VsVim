
namespace Vim
open Vim.Interpreter

type internal KeyMap =
    interface IKeyMap

    new: settings: IVimGlobalSettings * variableMap: VariableMap -> KeyMap 

type internal LocalAbbreviationMap =
    interface IVimLocalAbbreviationMap

    new: globalAbbreviationMap: IVimGlobalAbbreviationMap * wordUtil: WordUtil -> LocalAbbreviationMap

type internal GlobalAbbreviationMap =
    interface IVimGlobalAbbreviationMap

    new: unit -> GlobalAbbreviationMap

