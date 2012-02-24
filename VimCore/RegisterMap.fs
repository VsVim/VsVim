#light

namespace Vim

/// IRegisterValueBacking implementation for the clipboard 
type ClipboardRegisterValueBacking (_device : IClipboardDevice) =

    member x.RegisterValue = 
        let text = _device.Text
        let operationKind = 
            if EditUtil.GetLineBreakLengthAtEnd text > 0 then
                OperationKind.LineWise
            else
                OperationKind.CharacterWise
        RegisterValue.OfString text operationKind

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = _device.Text <- value.StringValue

/// IRegisterValueBacking implementation for append registers.  All of the lower
/// case letter registers can be accessed via an upper case version.  The only
/// difference is when accessed via upper case sets of the value should be 
/// append operations
type AppendRegisterValueBacking (_register : Register) =
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = _register.RegisterValue
            and set value = _register.RegisterValue <- _register.RegisterValue.Append value

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

        // Is this an append register 
        let isAppendRegister (name : RegisterName) = name.IsAppend

        let getBacking name = 
            match name with 
            | RegisterName.Unnamed -> clipboardBacking
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Register_Plus) -> clipboardBacking
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Register_Star) -> clipboardBacking
            | RegisterName.ReadOnly(ReadOnlyRegister.Register_Percent) -> fileNameBacking
            | _ -> DefaultRegisterValueBacking() :> IRegisterValueBacking

        // Create the map without the append registers
        let map = 
            RegisterName.All
            |> Seq.filter (fun name -> not (isAppendRegister name))
            |> Seq.map (fun n -> n, Register(n, getBacking n))
            |> Map.ofSeq

        // Now that the map has all of the append registers backing add the actual append
        // registers
        let map = 
            let originalMap = map
            let names =  RegisterName.All |> Seq.filter isAppendRegister
            let foldFunc map (name : RegisterName) =
                let backingName = 
                    name.Char 
                    |> Option.get 
                    |> CharUtil.ToLower 
                    |> NamedRegister.OfChar
                    |> Option.get
                    |> RegisterName.Named
                let backingRegister = Map.find backingName originalMap
                let value = AppendRegisterValueBacking(backingRegister)
                let register = Register(name, value)
                Map.add name register map
            Seq.fold foldFunc originalMap names 

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
          

