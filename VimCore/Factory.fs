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

    let CreateVimBuffer host view name = 
        let map = new RegisterMap()
        let data = VimBufferData(name, view, host, map)
        let buf = VimBuffer(data, (modeMap data))
        buf :> IVimBuffer
