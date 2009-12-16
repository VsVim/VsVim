#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Default implementation of IVim 
type internal Vim(_host : IVimHost) =
    let _data = VimData()
    let mutable _buffers : seq<IVimBuffer> = Seq.empty

    member x.CreateVimBufferCore view editOperations name caret = 
        let data = VimBufferData(name, view, _host, _data :> IVimData, caret, editOperations ) :> IVimBufferData
        let opts = Modes.Normal.DefaultOperations() :> Modes.Normal.IOperations
        let modeList = 
            [
                ((Modes.Normal.NormalMode(data, opts)) :> IMode);
                ((Modes.Command.CommandMode(data)) :> IMode);
                ((Modes.Insert.InsertMode(data)) :> IMode);
            ]
        let modeMap =
            modeList 
                |> List.map (fun x-> (x.ModeKind,x))
                |> Map.ofList
        let buf = VimBuffer(data, modeMap)
        buf :> IVimBuffer
    
    interface IVim with
        member x.Host = _host
        member x.Data = _data :> IVimData
        member x.Buffers = _buffers
        member x.CreateBuffer view editOperations name caret =
            x.CreateVimBufferCore view editOperations name caret 
        
        
