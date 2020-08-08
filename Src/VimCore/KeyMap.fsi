namespace Vim

open Vim.Interpreter


type internal GlobalKeyMap =
    interface IVimGlobalKeyMap

    new: variableMap:VariableMap -> GlobalKeyMap

type internal LocalKeyMap =
    interface IVimLocalKeyMap

    new: globalKeyMap:IVimGlobalKeyMap * globalSettings:IVimGlobalSettings * variableMap:VariableMap -> LocalKeyMap

type internal GlobalAbbreviationMap =
    interface IVimGlobalAbbreviationMap

    new: unit -> GlobalAbbreviationMap

type internal LocalAbbreviationMap =
    interface IVimLocalAbbreviationMap

    new: keyMap:IVimLocalKeyMap * globalAbbreviationMap:IVimGlobalAbbreviationMap * wordUtil:WordUtil
         -> LocalAbbreviationMap
