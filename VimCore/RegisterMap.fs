#light

namespace Vim

/// IRegisterValueBacking implementation for the clipboard 
type ClipboardRegisterValueBacking ( _device : IClipboardDevice ) =
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = RegisterValue.OfString _device.Text OperationKind.LineWise
            and set value = _device.Text <- value.StringValue

type internal RegisterMap (_map: Map<RegisterName,Register> ) =
    new( clipboard : IClipboardDevice, currentFileNameFunc : unit -> string option ) = 
        let clipboardBacking = ClipboardRegisterValueBacking(clipboard) :> IRegisterValueBacking
        let fileNameBacking = { new IRegisterValueBacking with
            member x.RegisterValue
                with get() = 
                    let text = 
                        match currentFileNameFunc() with
                        | None -> StringUtil.empty
                        | Some(str) -> str
                    RegisterValue.OfString text OperationKind.CharacterWise
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
    member x.SetRegisterValue (reg : Register) regOperation value = 
        if reg.Name <> RegisterName.Blackhole then
            reg.RegisterValue <- value

            // If this is not the unnamed register then the unnamed register needs to 
            // be updated 
            if reg.Name <> RegisterName.Unnamed then
                let unnamedReg = x.GetRegister RegisterName.Unnamed
                unnamedReg.RegisterValue <- value

            // Update the numbered register based on the type of the operation
            match regOperation with
            | RegisterOperation.Delete ->
                // Update the numbered registers with the new values.  First shift the existing
                // values up the stack
                let intToName num = 
                    let c = char (num + (int '0'))
                    let name = NumberedRegister.OfChar c |> Option.get
                    RegisterName.Numbered name
        
                // Next is insert the new value into the numbered register list.  New value goes
                // into 1 and the rest shift up
                for i in [9;8;7;6;5;4;3;2] do
                    let cur = intToName i |> x.GetRegister
                    let prev = intToName (i-1) |> x.GetRegister
                    cur.RegisterValue <- prev.RegisterValue
                let regOne = x.GetRegister (RegisterName.Numbered NumberedRegister.Register_1)
                regOne.RegisterValue <- value
            | RegisterOperation.Yank ->

                // If the yank occurs to the unnamed register then update register 0 with the 
                // value
                if reg.Name = RegisterName.Unnamed then
                    let regZero = x.GetRegister (RegisterName.Numbered NumberedRegister.Register_0)
                    regZero.RegisterValue <- value
    
            // Possibly update the small delete register
            if reg.Name <> RegisterName.Unnamed && regOperation = RegisterOperation.Delete then
                match value.StringData with
                | StringData.Block(_) -> ()
                | StringData.Simple(str) -> 
                    if not (StringUtil.containsChar str '\n') then
                        let regSmallDelete = x.GetRegister RegisterName.SmallDelete
                        regSmallDelete.RegisterValue <- value

    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue register operation value = x.SetRegisterValue register operation value
          

