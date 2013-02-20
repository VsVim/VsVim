
namespace Vim

/// Records our current history search information
[<RequireQualifiedAccess>]
type HistoryState =
    | Empty 
    | Index of (string list) * int

type HistoryUtilData<'TData, 'TResult> = {

    ClientData : 'TData

    HistoryClient : IHistoryClient<'TData, 'TResult>

    HistoryState : HistoryState
}

[<RequireQualifiedAccess>]
type HistoryCommand =
    | Previous
    | Next
    | Execute
    | Cancel
    | Back
    | Edit of Path

type internal HistoryUtil ()  =

    static let _keyInputMap = 

        let other = 
            [
                (KeyNotationUtil.StringToKeyInput "<C-p>", HistoryCommand.Previous)
                (KeyNotationUtil.StringToKeyInput "<C-n>", HistoryCommand.Next)
            ]

        seq {
            yield ("<Enter>", HistoryCommand.Execute)
            yield ("<Up>", HistoryCommand.Previous)
            yield ("<Down>", HistoryCommand.Next)
            yield ("<BS>", HistoryCommand.Back)
            yield ("<BS>", HistoryCommand.Back)
            yield ("<Left>", HistoryCommand.Edit Path.Backward)
            yield ("<Right>", HistoryCommand.Edit Path.Backward)
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
        |> Seq.append other
        |> Map.ofSeq

    static member CommandNames = _keyInputMap |> MapUtil.keys |> List.ofSeq

    static member Begin<'TData, 'TResult> (historyClient : IHistoryClient<'TData, 'TResult>) data command : BindDataStorage<'TResult> = 

        let data = { 
            ClientData = data
            HistoryClient = historyClient
            HistoryState = HistoryState.Empty 
        }

        BindDataStorage.Complex (fun () ->
            let func = HistoryUtil.Process data command
            { KeyRemapMode = historyClient.RemapMode; BindFunction = func })

    /// Process a single KeyInput for the IHistoryClient
    static member Process (data: HistoryUtilData<_, _>) command (keyInput: KeyInput) =

        let historyClient = data.HistoryClient
        let processCommand command =
            let clientData = historyClient.ProcessCommand data.ClientData command
            let data = { data with ClientData = clientData }
            BindResult<_>.CreateNeedMoreInput historyClient.RemapMode (HistoryUtil.Process data command)

        match Map.tryFind keyInput _keyInputMap with
        | Some HistoryCommand.Execute ->
            // Enter key completes the action
            let result = historyClient.Completed data.ClientData command
            historyClient.HistoryList.Add command
            BindResult.Complete result
        | Some HistoryCommand.Cancel ->
            // Escape cancels the current search.  It does update the history though
            historyClient.Cancelled data.ClientData
            historyClient.HistoryList.Add command
            BindResult.Cancelled
        | Some HistoryCommand.Back ->
            match command.Length with
            | 0 -> 
                historyClient.Cancelled data.ClientData
                BindResult.Cancelled
            | _ -> 
                let command = command.Substring(0, command.Length - 1)
                processCommand command
        | Some HistoryCommand.Previous ->
            HistoryUtil.ProcessPrevious data command
        | Some HistoryCommand.Next ->
            HistoryUtil.ProcessNext data command
        | Some (HistoryCommand.Edit _) ->
            // TODO: We will be implementing command line editing at some point.  In the mean
            // time though don't process these keys as they don't have real character 
            // representations and will show up as squares.  Just beep to let the user 
            // know we don't support it
            historyClient.Beep()
            BindResult<_>.CreateNeedMoreInput historyClient.RemapMode (HistoryUtil.Process data command)
        | None -> 
            let command = command + (keyInput.Char.ToString())
            processCommand command

    /// Run a history scroll at the specified index
    static member DoHistoryScroll (data : HistoryUtilData<_, _>) command (historyList : string list) index =
        if index < 0 || index >= historyList.Length then
            // Make sure we are searching at a valid index
            data.HistoryClient.Beep()
            data, command
        else
            // Update the search to be this specific item
            let command = List.nth historyList index
            let clientData = data.HistoryClient.ProcessCommand data.ClientData command
            let data = { data with ClientData = clientData; HistoryState = HistoryState.Index (historyList, index) }
            data, command

    /// Provide the previous entry in the list.  This will initiate a scrolling operation
    static member ProcessPrevious (data : HistoryUtilData<_, _>) command =
        let data, command = 
            match data.HistoryState with
            | HistoryState.Empty ->
                let list = 
                    if not (StringUtil.isNullOrEmpty command) then
                        data.HistoryClient.HistoryList
                        |> Seq.filter (fun value -> StringUtil.startsWith command value)
                        |> List.ofSeq
                    else
                        data.HistoryClient.HistoryList.Items
                HistoryUtil.DoHistoryScroll data command list 0
            | HistoryState.Index (list, index) -> 
                HistoryUtil.DoHistoryScroll data command list (index + 1)
        BindResult<_>.CreateNeedMoreInput data.HistoryClient.RemapMode (HistoryUtil.Process data command)

    /// Provide the next entry in the list.  This will initiate a scolling operation
    static member ProcessNext (data : HistoryUtilData<_, _>) command =
        let data, command = 
            match data.HistoryState with
            | HistoryState.Empty ->
                data.HistoryClient.Beep()
                data, command
            | HistoryState.Index (list, index) -> 
                if index = 0 then
                    let clientData = data.HistoryClient.ProcessCommand data.ClientData ""
                    let data = { data with ClientData = clientData; HistoryState = HistoryState.Empty }
                    data, ""
                else
                    HistoryUtil.DoHistoryScroll data command list (index - 1)

        BindResult<_>.CreateNeedMoreInput data.HistoryClient.RemapMode (HistoryUtil.Process data command)

