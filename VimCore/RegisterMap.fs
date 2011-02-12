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
                    { Value=StringData.Simple text; OperationKind=OperationKind.CharacterWise }
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

    /// Updates the given register with the specified value.  This will also update 
    /// other registers based on the type of update that is being performed.  See 
    /// :help registers for the full details
    member x.SetRegisterValue (reg:Register) regOperation value = 
        if reg.Name <> RegisterName.Blackhole then
            reg.Value <- value

            // If this is not the unnamed register then the unnamed register needs to 
            // be updated 
            if reg.Name <> RegisterName.Unnamed then
                let unnamedReg = x.GetRegister RegisterName.Unnamed
                unnamedReg.Value <- value

            // Update the numbered registers with the new values.  First shift the existing
            // values up the stack
            let intToName num = 
                let c = char (num + (int '0'))
                let name = NumberedRegister.OfChar c |> Option.get
                RegisterName.Numbered name
    
            // Next is insert the new value into the numbered register list.  New value goes
            // into 0 and the rest shift up
            for i in [9;8;7;6;5;4;3;2;1] do
                let cur = intToName i |> x.GetRegister
                let prev = intToName (i-1) |> x.GetRegister
                cur.Value <- prev.Value
            let regZero = x.GetRegister (RegisterName.Numbered NumberedRegister.Register_0)
            regZero.Value <- value
    
            // Possibily update the small delete register
            if reg.Name <> RegisterName.Unnamed && regOperation = RegisterOperation.Delete then
                match value.Value with
                | StringData.Block(_) -> ()
                | StringData.Simple(str) -> 
                    if not (StringUtil.containsChar str '\n') then
                        let regSmallDelete = x.GetRegister RegisterName.SmallDelete
                        regSmallDelete.Value <- value

    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue register operation value = x.SetRegisterValue register operation value
          

