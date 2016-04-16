#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition
open System.Collections.Generic
open StringBuilderExtensions
open Vim
open Vim.Interpreter
open System.Text.RegularExpressions
open VimCoreExtensions

[<Sealed>]
[<Export(typeof<IVimBufferCreationListener>)>]
type internal AutoCommandRunner
    [<ImportingConstructor>]
    (
        _vim : IVim
    ) as this =

    let _vimData = _vim.VimData
    let _vimHost = _vim.VimHost
    let _globalSettings = _vim.GlobalSettings
    
    do 
        _vim.VimHost.ActiveTextViewChanged
        |> Observable.add this.OnActiveTextViewChanged

    /// Create the Regex for the specified pattern.  The allowed items are specified in ':help autocmd-patterns'
    static let CreateFilePatternRegex (pattern : string) =
        let isFileChar c =
            System.Char.IsLetterOrDigit c &&
            System.IO.Path.GetInvalidFileNameChars() |> Array.forall(fun i -> i <> c)
        let builder = System.Text.StringBuilder()
        let mutable i = 0
        let mutable nested = 0
        while i < pattern.Length do
            match pattern.[i] with
            | '*' -> builder.AppendString ".*"
            | '.' -> builder.AppendString "\."
            | '?' -> builder.AppendChar '.'
            | '/' -> builder.AppendString "[\\/]"
            | '\\' ->
                if i + 1 < pattern.Length then
                    let p = pattern.[i]
                    let p1 = pattern.[i + 1]
                    if p1 = '(' || p1 = ')' || p1 = '|' then
                        builder.AppendChar p1
                        i <- i + 1
                    elif p1 = '?' then
                        builder.AppendChar p
                        builder.AppendChar p1
                        i <- i + 1
                    elif p1 = ',' || p1 = '}' || p1 = '{' || p1 = '%' || p1 = '#' then
                        builder.AppendChar p1
                        i <- i + 1
                    elif p1 = '\\' && i + 2 <= pattern.Length && pattern.[i + 2] = '{' then
                        builder.AppendChar '{'
                        i <- i + 2
                    elif p1 <> '+' && (isFileChar p1 || p1 = '*' || p1 = '?') then
                        builder.AppendString "[\\/]"
                    else
                        builder.AppendChar p
                        builder.AppendChar p1
                        i <- i + 1
                else
                    builder.AppendChar '\\'
            | '{' ->
                builder.AppendChar '('
                nested <- nested + 1
            | '}' ->
                builder.AppendChar ')'
                nested <- nested - 1
            | ',' ->
                if nested > 0 then builder.AppendChar '|'
                else builder.AppendChar ','
            | _ ->
                builder.AppendChar pattern.[i]
            i <- i + 1

        builder.AppendChar '$'

        let bclPattern = builder.ToString()
        VimRegexFactory.CreateBcl bclPattern RegexOptions.None

    static let splitPattern (pattern:string) =
        let builder = System.Text.StringBuilder()
        let mutable i = 0
        let mutable nested = 0
        let xs = List<string>()
        while i < pattern.Length do
            match pattern.[i] with
            | '{' ->
                builder.AppendChar '{'
                nested <- nested + 1
            | '}' ->
                builder.AppendChar '}'
                nested <- nested - 1
            | ',' ->
                if nested > 0 then builder.AppendChar ','
                else
                    xs.Add(builder.ToString())
                    builder.Clear() |> ignore
            | _ as x ->
                builder.AppendChar x
            i <- i + 1
        xs.Add(builder.ToString())
        xs

    static let FileNameEndsWithPattern (fileName:string) pattern =
        try
            let isMatch p =
                let regex = CreateFilePatternRegex p
                match regex with
                | None -> false
                | Some regex -> regex.IsMatch (fileName.Replace("\\","/"))

            splitPattern pattern
            |> Seq.exists isMatch
        with
            _ -> false

    member x.GetAutoCommands fileName eventKind =
        _vimData.AutoCommands
        |> Seq.filter (fun autoCommand -> autoCommand.EventKind = eventKind)
        |> Seq.filter (fun autoCommand -> FileNameEndsWithPattern fileName autoCommand.Pattern)
        |> List.ofSeq

    /// Run the specified AutoCommand against the IVimBuffer in question 
    member x.RunAutoCommands (vimBuffer : IVimBuffer) eventKind = 
        if _vimHost.IsAutoCommandEnabled then
            let fileName = _vimHost.GetName vimBuffer.TextBuffer
            let autoCommandList = x.GetAutoCommands fileName eventKind
            if autoCommandList.Length > 0 then
                let vimInterpreter = _vim.GetVimInterpreter vimBuffer
                let parser = Parser(_globalSettings, _vimData)

                autoCommandList
                |> Seq.iter (fun autoCommand -> 
                    parser.ParseLineCommand autoCommand.LineCommandText 
                    |> vimInterpreter.RunLineCommand
                    |> ignore)

    /// Called when the active ITextView changes according to the host
    member x.OnActiveTextViewChanged (e : TextViewChangedEventArgs) =
        match OptionUtil.map2 _vim.GetVimBuffer e.OldTextView with
        | Some vimBuffer -> x.RunAutoCommands vimBuffer EventKind.BufLeave
        | None -> ()

        match OptionUtil.map2 _vim.GetVimBuffer e.NewTextView with
        | Some vimBuffer -> x.RunAutoCommands vimBuffer EventKind.BufEnter
        | None -> ()

    /// VimBufferCreated is the closest event we have for BufEnter.   
    member x.OnVimBufferCreated (vimBuffer : IVimBuffer) =
        // TODO: BufEnter should really be raised every time the text buffer gets edit
        // focus.  Hard to detect that in WPF / VS though because keyboard focus is very
        // different than edit focus.  For now just raise it once here.  
        x.RunAutoCommands vimBuffer EventKind.BufEnter

        x.RunAutoCommands vimBuffer EventKind.FileType

        x.RunAutoCommands vimBuffer EventKind.BufWinEnter

        let bag = DisposableBag()
        vimBuffer.Closing
        |> Observable.subscribe (fun _ -> 
            x.RunAutoCommands vimBuffer EventKind.BufWinLeave
            bag.DisposeAll())
        |> bag.Add

    interface IVimBufferCreationListener with
        member x.VimBufferCreated vimBuffer = x.OnVimBufferCreated vimBuffer

    
