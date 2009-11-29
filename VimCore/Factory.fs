#light


namespace VimCore
open Microsoft.VisualStudio.Text

module Factory =
    let modeList (data : VimBufferData) : list<IMode> = 
        [
            ((new Modes.Normal.NormalMode(data)) :> IMode);
            ((new Modes.Command.CommandMode(data)) :> IMode);
            ((new Modes.Insert.InsertMode(data)) :> IMode);
        ]
    let modeMap data = 
        modeList data
            |> List.map (fun x-> (x.ModeKind,x))
            |> Map.ofList

    let CreateVimBuffer host view = 
        let map = new RegisterMap()
        let data = VimBufferData(view, host, map)
        let buf = VimBuffer(data, (modeMap data))
        let data = VimBufferData(view, host, map)
        buf :> IVimBuffer
