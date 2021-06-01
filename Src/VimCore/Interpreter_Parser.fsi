﻿#light

namespace Vim.Interpreter
open Vim

[<RequireQualifiedAccess>]
type internal ParseResult<'T> = 
    | Succeeded of Value: 'T
    | Failed of Error: string

[<Sealed>]
[<Class>]
type internal Parser = 

    new: globalSettings: IVimGlobalSettings * vimData: IVimData -> Parser

    new: globalSettings: IVimGlobalSettings * vimData: IVimData * lines: string[] -> Parser

    member IsDone: bool

    member ContextLineNumber: int

    /// Parse the next complete command from the source.  Command pairs like :func and :endfunc
    /// will be returned as a single Function command.  
    member ParseNextCommand: unit -> LineCommand

    /// Parse the next line from the source.  Command pairs like :func and :endfunc will
    /// not be returned as a single command.  Instead they will be returned as the individual
    /// items
    member ParseNextLine: unit -> LineCommand

    member ParseRange: rangeText: string -> LineRangeSpecifier * string

    member ParseExpression: expressionText: string -> ParseResult<Expression>

    member ParseLineCommand: commandText: string -> LineCommand

    member ParseLineCommands: lines: string[] -> LineCommand list

    /// This will expand out an abbreviated command name to the full name. For example 
    /// will expand 'e' to 'edit'.
    member TryExpandCommandName: shortCommandName: string -> string option
