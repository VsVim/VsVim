#light

namespace VimCore
open Microsoft.VisualStudio.Text.Editor

/// Default implementation of IVim 
type internal Vim() =
    let _data = VimData()
    let mutable _buffers : seq<IVimBuffer> = Seq.empty

    member x.CreateVimBufferCore host view name caret = 
        let data = VimBufferData(name, view, host, _data :> IVimData, caret )
        let modeList = 
            [
                ((new Modes.Normal.NormalMode(data)) :> IMode);
                ((new Modes.Command.CommandMode(data)) :> IMode);
                ((new Modes.Insert.InsertMode(data)) :> IMode);
            ]
        let modeMap =
            modeList 
                |> List.map (fun x-> (x.ModeKind,x))
                |> Map.ofList
        let buf = VimBuffer(data, modeMap)
        buf :> IVimBuffer
    
    interface IVim with
        member x.Data = _data :> IVimData
        member x.Buffers = _buffers
        member x.CreateBuffer host view name caret =
            x.CreateVimBufferCore host view name caret
        
        
