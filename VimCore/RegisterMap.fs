#light

namespace Vim

/// IRegisterValueBacking implementation for the clipboard 
type ClipboardRegisterValueBacking ( _device : IClipboardDevice ) =
    interface IRegisterValueBacking with
        member x.Value 
            with get () = RegisterValue.CreateFromText _device.Text
            and set value = _device.Text <- value.Value.String

type internal RegisterMap (_map: Map<RegisterName,Register> ) =
    new( clipboard : IClipboardDevice, currentFileNameFunc : unit -> string option ) = 
        let clipboardBacking = ClipboardRegisterValueBacking(clipboard) :> IRegisterValueBacking
        let fileNameBacking = { new IRegisterValueBacking with
            member x.Value
                with get() = 
                    let text = 
                        match currentFileNameFunc() with
                        | None -> StringUtil.empty
                        | Some(str) -> str
                    RegisterValue.CreateFromText text
                and set _ = () }

        let getBacking name = 
            match name with 
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Register_Plus) -> clipboardBacking
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Register_Star) -> clipboardBacking
            | RegisterName.ReadOnly(ReadOnlyRegister.Register_Percent) -> fileNameBacking
            | _ -> DefaultRegisterValueBacking() :> IRegisterValueBacking
        let map = 
            RegisterNameUtil.RegisterNames
            |> Seq.map (fun n -> n,Register(n, getBacking n))
            |> Map.ofSeq
        RegisterMap(map)

    member x.GetRegister name = Map.find name _map
    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
            
          

