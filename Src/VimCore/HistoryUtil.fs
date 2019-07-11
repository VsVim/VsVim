
namespace Vim

/// Records our current history search information
[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type HistoryState =
    | Empty 
    | Index of HistoryList: (string list) * Index: int

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type HistoryCommand =
    | Home
    | End
    | Left
    | Right
    | Previous
    | Next
    | Execute
    | Cancel
    | Backspace
    | Delete
    | Paste
    | PasteSpecial of WordKind: WordKind
    | Clear

type internal HistorySession<'TData, 'TResult>
    (
        _historyClient: IHistoryClient<'TData, 'TResult>,
        _initialClientData: 'TData,
        _command: EditableCommand,
        _localAbbreviationMap: IVimLocalAbbreviationMap,
        _motionUtil: IMotionUtil,
        _allowAbbreviations: bool
    ) =

    let _registerMap = _historyClient.RegisterMap
    let mutable _command = _command
    let mutable _clientData = _initialClientData
    let mutable _inPasteWait = false
    let mutable _historyState = HistoryState.Empty

    member x.ResetCommand command = 
        // Run the given command through the IHistoryClient and update our data based on the 
        // information returned 
        _clientData <- _historyClient.ProcessCommand _clientData command
        _command <- command
        _inPasteWait <- false

    member x.CreateBindResult() = 
        let bindData = { KeyRemapMode = _historyClient.RemapMode; MappedBindFunction = x.Process } 
        MappedBindResult<_>.NeedMoreInput bindData

    member x.CreateBindDataStorage() = 
        MappedBindDataStorage.Complex (fun () -> { KeyRemapMode = _historyClient.RemapMode; MappedBindFunction = x.Process })

    member x.ProcessCore(keyInputData: KeyInputData, suppressAbbreviations: bool) =
        let keyInput = keyInputData.KeyInput
        let wasMapped = keyInputData.WasMapped
        match Map.tryFind keyInput HistoryUtil.KeyInputMap with
        | Some HistoryCommand.Execute ->

            // Enter key completes the action and updates the history if not
            // mapped.
            let result = _historyClient.Completed _clientData _command wasMapped
            if not wasMapped then
                _historyClient.HistoryList.Add _command.Text
            _inPasteWait <- false
            MappedBindResult.Complete result
        | Some HistoryCommand.Cancel ->

            // Escape cancels the current search and updates the history if not
            // mapped.
            _historyClient.Cancelled _clientData
            if not wasMapped then
                _historyClient.HistoryList.Add _command.Text
            _inPasteWait <- false
            MappedBindResult.Cancelled
        | Some HistoryCommand.Home ->
            _command.Home()
            |> x.ResetCommand
            x.CreateBindResult()
        | Some HistoryCommand.End ->
            _command.End()
            |> x.ResetCommand
            x.CreateBindResult()
        | Some HistoryCommand.Left ->
            _command.Left()
            |> x.ResetCommand
            x.CreateBindResult()
        | Some HistoryCommand.Right ->
            _command.Right()
            |> x.ResetCommand
            x.CreateBindResult()
        | Some HistoryCommand.Backspace ->
            match _command.Text.Length with
            | 0 -> 
                _historyClient.Cancelled _clientData
                MappedBindResult.Cancelled
            | _ -> 
                _command.Backspace()
                |> x.ResetCommand
                x.CreateBindResult()
        | Some HistoryCommand.Delete ->
            _command.Delete()
            |> x.ResetCommand
            x.CreateBindResult()
        | Some HistoryCommand.Previous ->
            x.ProcessPrevious()
        | Some HistoryCommand.Next ->
            x.ProcessNext()
        | Some HistoryCommand.Paste ->
            _inPasteWait <- true
            x.CreateBindResult()
        | Some (HistoryCommand.PasteSpecial wordKind) ->
            x.ProcessPasteSpecial wordKind
        | Some HistoryCommand.Clear ->
            x.ResetCommand EditableCommand.Empty
            x.CreateBindResult()
        | None -> 
            x.ProcessNormal(keyInputData, suppressAbbreviations)
            x.CreateBindResult()

    /// Process a single KeyInput value in the state machine. 
    member x.Process (keyInputData: KeyInputData) = x.ProcessCore(keyInputData, suppressAbbreviations = false)

    member x.ProcessPasteCore (keyInput: KeyInput) = 
        match RegisterName.OfChar keyInput.Char with
        | None -> ()
        | Some name -> 
            let register = _registerMap.GetRegister name
            register.StringValue
            |> _command.InsertText
            |> x.ResetCommand

        _inPasteWait <- false

    member x.ProcessPaste keyInput = 
        x.ProcessPasteCore keyInput
        x.CreateBindResult()

    member x.ProcessPasteSpecial wordKind =
        if _inPasteWait then
            x.ResetCommand EditableCommand.Empty
            MappedBindResult<_>.Error
        else
            let motion = Motion.InnerWord wordKind
            let arg = MotionArgument(MotionContext.AfterOperator)
            let currentWord = _motionUtil.GetMotion motion arg
            match currentWord with
            | None -> x.ResetCommand _command
            | Some cw ->
                cw.Span.GetText()
                |> _command.InsertText
                |> x.ResetCommand

            x.CreateBindResult()

    /// Run a history scroll at the specified index
    member x.DoHistoryScroll (historyList: string list) index =
        if index < 0 || index >= historyList.Length then
            // Make sure we are searching at a valid index
            _historyClient.Beep()
        else
            // Update the search to be this specific item
            let command = List.item index historyList
            _command <- EditableCommand(command)
            _clientData <- _historyClient.ProcessCommand _clientData _command
            _historyState <- HistoryState.Index (historyList, index)

    /// Provide the previous entry in the list.  This will initiate a scrolling operation
    member x.ProcessPrevious() =
        match _historyState with 
        | HistoryState.Empty ->
            let list = 
                if not (StringUtil.IsNullOrEmpty _command.Text) then
                    _historyClient.HistoryList
                    |> Seq.filter (fun value -> StringUtil.StartsWith _command.Text value)
                    |> List.ofSeq
                else
                    _historyClient.HistoryList.Items
            x.DoHistoryScroll list 0
        | HistoryState.Index (list, index) -> 
            x.DoHistoryScroll list (index + 1)

        x.CreateBindResult()

    /// Provide the next entry in the list.  This will initiate a scrolling operation
    member x.ProcessNext() = 
        match _historyState with
        | HistoryState.Empty ->
            _historyClient.Beep()
        | HistoryState.Index (list, index) -> 
            if index = 0 then
                _command <- EditableCommand.Empty
                _clientData <- _historyClient.ProcessCommand _clientData _command
                _historyState <- HistoryState.Empty
            else
                x.DoHistoryScroll list (index - 1)

        x.CreateBindResult()

    member x.ProcessNormal(keyInputData: KeyInputData, suppressAbbreviations: bool) =
        let keyInput = keyInputData.KeyInput
        if _inPasteWait then
            x.ProcessPasteCore keyInput
        elif not suppressAbbreviations && _allowAbbreviations && _command.CaretPosition = _command.Text.Length then
            match _localAbbreviationMap.TryAbbreviate _command.Text keyInput AbbreviationMode.Command with
            | None -> ()
            | Some result -> 
                let text = _command.Text.Substring(0, _command.Text.Length - result.ReplacedSpan.Length)
                x.ResetCommand (EditableCommand(text))
                for keyInput in result.Replacement.KeyInputs do 
                    let keyInputData = KeyInputData.Create keyInput false
                    x.ProcessCore(keyInputData, suppressAbbreviations = true) |> ignore

            x.ProcessCore(keyInputData, suppressAbbreviations = true) |> ignore
        elif CharUtil.IsPrintable keyInput.Char && not keyInput.HasKeyModifiers then
            keyInput.Char.ToString()
            |> _command.InsertText
            |> x.ResetCommand

    member x.Cancel() = 
        _historyClient.Cancelled _clientData

    interface IHistorySession<'TData, 'TResult> with 
        member x.HistoryClient = _historyClient
        member x.EditableCommand = _command
        member x.ClientData = _clientData
        member x.InPasteWait = _inPasteWait
        member x.CreateBindDataStorage() = x.CreateBindDataStorage()
        member x.Cancel() = x.Cancel()
        member x.ResetCommand command = x.ResetCommand command

and internal HistoryUtil ()  =

    static let _keyInputMap = 

        let set1 = 
            seq { 
                yield ("<Home>", HistoryCommand.Home)
                yield ("<End>", HistoryCommand.End)
                yield ("<Left>", HistoryCommand.Left)
                yield ("<Right>", HistoryCommand.Right)
                yield ("<C-p>", HistoryCommand.Previous)
                yield ("<C-n>", HistoryCommand.Next)
                yield ("<C-R>", HistoryCommand.Paste)
                yield ("<C-U>", HistoryCommand.Clear)
                yield ("<C-w>", HistoryCommand.PasteSpecial WordKind.NormalWord)
                yield ("<C-a>", HistoryCommand.PasteSpecial WordKind.BigWord)
            }
            |> Seq.map (fun (notation, cmd) -> (KeyNotationUtil.StringToKeyInput notation, cmd))

        let set2 =
            seq {
                yield ("<Enter>", HistoryCommand.Execute)
                yield ("<Up>", HistoryCommand.Previous)
                yield ("<Down>", HistoryCommand.Next)
                yield ("<BS>", HistoryCommand.Backspace)
                yield ("<Del>", HistoryCommand.Delete)
                yield ("<Esc>", HistoryCommand.Cancel)
            }
            |> Seq.map (fun (name, command) -> 
                // Vim itself distinguishes between items like <BS> and <S-BS> and this can be verified by using
                // key mappings.  At the history level though these commands are treated equally so we just add
                // the variations into the map
                let keyInput1 = KeyNotationUtil.StringToKeyInput name
                let keyInput2 = KeyInputUtil.ApplyKeyModifiers keyInput1 VimKeyModifiers.Shift
                let keyInput3 = KeyInputUtil.ApplyKeyModifiers keyInput1 VimKeyModifiers.Control
                let keyInput4 = KeyInputUtil.ApplyKeyModifiers keyInput1 (VimKeyModifiers.Shift ||| VimKeyModifiers.Control)
                seq { 
                    yield (keyInput1, command)    
                    yield (keyInput2, command)    
                    yield (keyInput3, command)    
                    yield (keyInput4, command)    
                })
            |> Seq.concat

        Seq.append set1 set2 |> Map.ofSeq

    static member CommandNames = _keyInputMap |> MapUtil.keys |> List.ofSeq

    static member KeyInputMap = _keyInputMap

    static member CreateHistorySession<'TData, 'TResult> historyClient clientData command localAbbreviationMap motionUtil allowAbbreviations =
        let historySession = HistorySession<'TData, 'TResult>(historyClient, clientData, command, localAbbreviationMap, motionUtil, allowAbbreviations)
        historySession :> IHistorySession<'TData, 'TResult>

