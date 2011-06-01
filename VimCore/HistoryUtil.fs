
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

type internal HistoryUtil ()  =

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

        if keyInput = KeyInputUtil.EnterKey then
            // Enter key completes the action
            let result = historyClient.Completed data.ClientData command
            historyClient.HistoryList.Add command
            BindResult.Complete result
        elif keyInput = KeyInputUtil.EscapeKey then
            // Escape cancels the current search.  It does update the history though
            historyClient.Cancelled data.ClientData
            historyClient.HistoryList.Add command
            BindResult.Cancelled
        elif keyInput.Key = VimKey.Back then
            match command.Length with
            | 0 -> 
                historyClient.Cancelled data.ClientData
                BindResult.Cancelled
            | _ -> 
                let command = command.Substring(0, command.Length - 1)
                processCommand command
        elif keyInput.Key = VimKey.Up then
            HistoryUtil.ProcessUp data command
        elif keyInput.Key = VimKey.Down then
            HistoryUtil.ProcessDown data command
        elif keyInput.Key = VimKey.Left || keyInput.Key = VimKey.Right then
            // TODO: We will be implementing command line editing at some point.  In the mean
            // time though don't process these keys as they don't have real character 
            // representations and will show up as squares.  Just beep to let the user 
            // know we don't support it
            historyClient.Beep()
            BindResult<_>.CreateNeedMoreInput historyClient.RemapMode (HistoryUtil.Process data command)
        else
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

    /// Process the up key.  This will initiate a scrolling operation
    static member ProcessUp (data : HistoryUtilData<_, _>) command =
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

    /// Process the down key during an incremental search
    static member ProcessDown (data : HistoryUtilData<_, _>) command =
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

