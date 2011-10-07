#light

namespace Vim.Modes.Command
open Vim
open Vim.Interpreter
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions
open Vim.RegexPatternUtil
open Vim.VimHostExtensions

[<System.Flags>]
type internal KeyRemapOptions =
    | None = 0
    | Buffer = 0x1
    | Silent = 0x2
    | Special = 0x4
    | Script = 0x8
    | Expr = 0x10
    | Unique = 0x20

module internal CommandParseUtil = 

    let rec SkipWhitespace (cmd:char list) =
        let inner head tail = 
            if System.Char.IsWhiteSpace head then SkipWhitespace (cmd |> List.tail)
            else cmd
        ListUtil.tryProcessHead cmd inner (fun () -> cmd)
        
    /// Skip past non-whitespace characters and return the string and next input
    let SkipNonWhitespace (cmd:char list) =
        let rec inner (cmd:char list) (data:char list) =
            let withHead headKey rest = 
                if System.Char.IsWhiteSpace headKey then (cmd,data)
                else inner rest (headKey :: data)
            ListUtil.tryProcessHead cmd withHead (fun () -> (cmd,data))
        let rest,data = inner cmd List.empty
        rest,(data |> List.rev |> StringUtil.ofCharSeq)

    /// Try and skip the ! operator
    let SkipBang (cmd:char list) =
        let inner head tail = 
            if head = '!' then (true, tail)
            else (false,cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (false,cmd))

    /// Parse the register out of the stream.  Will return default if no register is 
    /// specified
    let SkipRegister (map:IRegisterMap) (cmd:char list) =
        let defaultRegister = map.GetRegister RegisterName.Unnamed
        let inner head tail =
            match System.Char.IsDigit(head),RegisterNameUtil.CharToRegister head with
            | true,_ -> (defaultRegister, cmd)
            | false,Some(name)-> (map.GetRegister name, tail)
            | false,None -> (defaultRegister, cmd)
        ListUtil.tryProcessHead cmd inner (fun () -> (defaultRegister, cmd))

    let ParseKeyRemapOptions (rest:char list) =
        let rec inner (orig:char list) options =
            let rest,arg = orig |> SkipNonWhitespace
            match arg with
            | "<buffer>" -> inner rest (options ||| KeyRemapOptions.Buffer)
            | "<silent>" -> inner rest (options ||| KeyRemapOptions.Silent)
            | "<special>" -> inner rest (options ||| KeyRemapOptions.Special)
            | "<script>" -> inner rest (options ||| KeyRemapOptions.Script)
            | "<expr>" -> inner rest (options ||| KeyRemapOptions.Expr)
            | "<unique>" -> inner rest (options ||| KeyRemapOptions.Unique)
            | _ -> (orig |> SkipWhitespace,options)
        inner rest KeyRemapOptions.None

    /// Parse out the keys for a key remap command
    let ParseKeys (rest:char list) found notFound =
        let rest,options = rest |> ParseKeyRemapOptions
        let rest,left = rest |> SkipWhitespace |> SkipNonWhitespace
        let rest,right = rest |> SkipWhitespace |> SkipNonWhitespace 
        if System.String.IsNullOrEmpty(left) || System.String.IsNullOrEmpty(right) then
            notFound()
        else
            found left right rest

    /// Used to parse out the flags for substitute commands.  If successful 
    /// it will pass the flags and remaining characters to the 
    /// goodParse function and will call badParse on an error
    /// the flags.
    let ParseSubstituteFlags previousFlags rest = 

        // Convert the given char to a flag
        let charToFlag c = 
            match c with 
            | 'c' -> Some SubstituteFlags.Confirm
            | 'r' -> Some SubstituteFlags.UsePreviousSearchPattern
            | 'e' -> Some SubstituteFlags.SuppressError
            | 'g' -> Some SubstituteFlags.ReplaceAll
            | 'i' -> Some SubstituteFlags.IgnoreCase
            | 'I' -> Some SubstituteFlags.OrdinalCase
            | 'n' -> Some SubstituteFlags.ReportOnly
            | 'p' -> Some SubstituteFlags.PrintLast
            | 'l' -> Some SubstituteFlags.PrintLastWithList
            | '#' -> Some SubstituteFlags.PrintLastWithNumber
            | '&' -> Some SubstituteFlags.UsePreviousFlags
            | _  -> None

        // Iterate down the characters getting out the flags
        let rec inner rest flags isFirst = 

            match rest with
            | [] -> 
                // Nothing left so we are done
                flags, []
            | head :: tail -> 
                match charToFlag head with
                | None ->
                    // No flag then we're done
                    flags, rest
                | Some flag ->
                    if flag = SubstituteFlags.UsePreviousFlags && isFirst then
                        inner tail previousFlags false
                    elif flag = SubstituteFlags.UsePreviousFlags then
                        // Only valid in the first position.  
                        flags, rest
                    else 
                        let flags = flags ||| flag
                        inner tail flags false

        inner rest SubstituteFlags.None true

type CommandAction = char list -> SnapshotLineRange option -> bool -> RunResult

/// Type which is responsible for executing command mode commands
type internal CommandProcessor 
    ( 
        _vimBuffer : IVimBuffer,
        _operations : ICommonOperations,
        _fileSystem : IFileSystem,
        _foldManager : IFoldManager
    ) =

    let _vimBufferData = _vimBuffer.VimBufferData
    let _statusUtil = _vimBufferData.StatusUtil
    let _interpreter = Interpreter.Interpreter(_vimBuffer, _operations, _foldManager, _fileSystem)

    let mutable _command : System.String = System.String.Empty


    /// Parse out the range of the string and run the corresponding command
    member x.ParseAndRunInput (command : char list) =

        let text = StringUtil.ofCharList command
        match Parser.ParseLineCommand text with
        | ParseResult.Failed msg -> 
            _statusUtil.OnError msg
            RunResult.Completed
        | ParseResult.Succeeded lineCommand -> 
            _interpreter.RunLineCommand lineCommand

    /// Run the specified command.  This function can be called recursively
    member x.RunCommand (input: char list)=
        let prev = _command
        try
            // Strip off the preceding :
            let input = 
                match ListUtil.tryHead input with
                | None -> input
                | Some(head,tail) when head = ':' -> tail
                | _ -> input

            _command <- input |> StringUtil.ofCharSeq
            x.ParseAndRunInput input
        finally
            _command <- prev

    interface ICommandProcessor with
        member x.RunCommand input = x.RunCommand input

            


