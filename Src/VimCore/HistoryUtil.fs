
namespace Vim

/// Records our current history search information
[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type HistoryState =
    | Empty 
    | Index of (string list) * int

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type HistoryCommand =
    | Previous
    | Next
    | Execute
    | Cancel
    | Back
    | Paste
    | Clear

type internal HistorySession<'TData, 'TResult>
    (
        _historyClient : IHistoryClient<'TData, 'TResult>,
        _initialClientData : 'TData,
        _command : string
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
        BindResult<_>.CreateNeedMoreInput _historyClient.RemapMode x.Process

    member x.CreateBindDataStorage() = 
        BindDataStorage.Complex (fun () -> { KeyRemapMode = _historyClient.RemapMode; BindFunction = x.Process })

    /// Process a single KeyInput value in the state machine. 
    member x.Process (keyInput: KeyInput) =
        if _inPasteWait then
            x.ProcessPaste keyInput
        else
            x.ProcessCore keyInput

    member x.ProcessCore keyInput =
        match Map.tryFind keyInput HistoryUtil.KeyInputMap with
        | Some HistoryCommand.Execute ->
            // Enter key completes the action
            let result = _historyClient.Completed _clientData _command
            _historyClient.HistoryList.Add _command
            BindResult.Complete result
        | Some HistoryCommand.Cancel ->
            // Escape cancels the current search.  It does update the history though
            _historyClient.Cancelled _clientData
            _historyClient.HistoryList.Add _command
            BindResult.Cancelled
        | Some HistoryCommand.Back ->
            match _command.Length with
            | 0 -> 
                _historyClient.Cancelled _clientData
                BindResult.Cancelled
            | _ -> 
                let command = _command.Substring(0, _command.Length - 1)
                x.ResetCommand command
                x.CreateBindResult()
        | Some HistoryCommand.Previous ->
            x.ProcessPrevious()
        | Some HistoryCommand.Next ->
            x.ProcessNext()
        | Some HistoryCommand.Paste ->
            _inPasteWait <- true
            x.CreateBindResult()
        | Some HistoryCommand.Clear ->
            x.ResetCommand ""
            x.CreateBindResult()
        | None -> 
            let command = _command + (keyInput.Char.ToString())
            x.ResetCommand command
            x.CreateBindResult()

    member x.ProcessPaste keyInput = 
        match RegisterName.OfChar keyInput.Char with
        | None -> ()
        | Some name -> 
            let register = _registerMap.GetRegister name
            x.ResetCommand (_command + register.StringValue)

        _inPasteWait <- false
        x.CreateBindResult()

    /// Run a history scroll at the specified index
    member x.DoHistoryScroll (historyList : string list) index =
        if index < 0 || index >= historyList.Length then
            // Make sure we are searching at a valid index
            _historyClient.Beep()
        else
            // Update the search to be this specific item
            _command <- List.nth historyList index
            _clientData <- _historyClient.ProcessCommand _clientData _command
            _historyState <- HistoryState.Index (historyList, index)

    /// Provide the previous entry in the list.  This will initiate a scrolling operation
    member x.ProcessPrevious() =
        match _historyState with 
        | HistoryState.Empty ->
            let list = 
                if not (StringUtil.isNullOrEmpty _command) then
                    _historyClient.HistoryList
                    |> Seq.filter (fun value -> StringUtil.startsWith _command value)
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
                _clientData <- _historyClient.ProcessCommand _clientData ""
                _historyState <- HistoryState.Empty
            else
                x.DoHistoryScroll list (index - 1)

        x.CreateBindResult()

    member x.Cancel() = 
        _historyClient.Cancelled _clientData

    interface IHistorySession<'TData, 'TResult> with 
        member x.HistoryClient = _historyClient
        member x.Command = _command
        member x.ClientData = _clientData
        member x.InPasteWait = _inPasteWait
        member x.CreateBindDataStorage() = x.CreateBindDataStorage()
        member x.Cancel() = x.Cancel()
        member x.ResetCommand command = x.ResetCommand command

and internal HistoryUtil ()  =

    static let _keyInputMap = 

        let set1 = 
            seq { 
                yield ("<C-p>", HistoryCommand.Previous)
                yield ("<C-n>", HistoryCommand.Next)
                yield ("<C-R>", HistoryCommand.Paste)
                yield ("<C-U>", HistoryCommand.Clear)
            }
            |> Seq.map (fun (notation, cmd) -> (KeyNotationUtil.StringToKeyInput notation, cmd))

        let set2 =
            seq {
                yield ("<Enter>", HistoryCommand.Execute)
                yield ("<Up>", HistoryCommand.Previous)
                yield ("<Down>", HistoryCommand.Next)
                yield ("<BS>", HistoryCommand.Back)
                yield ("<Esc>", HistoryCommand.Cancel)
            }
            |> Seq.map (fun (name, command) -> 
                // Vim itself distinguishes between items like <BS> and <S-BS> and this can be verified by using
                // key mappings.  At the history level though these commands are treated equally so we just add
                // the variations into the map
                let keyInput1 = KeyNotationUtil.StringToKeyInput name
                let keyInput2 = KeyInputUtil.ApplyModifiers keyInput1 KeyModifiers.Shift
                let keyInput3 = KeyInputUtil.ApplyModifiers keyInput1 KeyModifiers.Control
                let keyInput4 = KeyInputUtil.ApplyModifiers keyInput1 (KeyModifiers.Shift ||| KeyModifiers.Control)
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

    static member CreateHistorySession<'TData, 'TResult> historyClient clientData command =
        let historySession = HistorySession<'TData, 'TResult>(historyClient, clientData, command)
        historySession :> IHistorySession<'TData, 'TResult>

