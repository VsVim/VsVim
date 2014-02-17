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
        RegisterValue(text, operationKind)

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

type LastSearchRegisterValueBacking (_vimData : IVimData) = 
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = RegisterValue(_vimData.LastSearchData.Pattern, OperationKind.CharacterWise)
            and set value = _vimData.LastSearchData <- SearchData(value.StringValue, Path.Forward, false)

type internal RegisterMap (_map: Map<RegisterName, Register>) =
    new(vimData : IVimData, clipboard : IClipboardDevice, currentFileNameFunc : unit -> string option) = 
        let clipboardBacking = ClipboardRegisterValueBacking(clipboard) :> IRegisterValueBacking
        let fileNameBacking = { new IRegisterValueBacking with
            member x.RegisterValue
                with get() = 
                    let text = 
                        match currentFileNameFunc() with
                        | None -> StringUtil.empty
                        | Some(str) -> str
                    RegisterValue(text, OperationKind.CharacterWise)
                and set _ = () }

        // Is this an append register 
        let isAppendRegister (name : RegisterName) = name.IsAppend

        let getBacking name = 
            match name with 
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Plus) -> clipboardBacking
            | RegisterName.SelectionAndDrop(SelectionAndDropRegister.Star) -> clipboardBacking
            | RegisterName.ReadOnly(ReadOnlyRegister.Percent) -> fileNameBacking
            | RegisterName.LastSearchPattern -> LastSearchRegisterValueBacking(vimData) :> IRegisterValueBacking
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
            RegisterName.All
            |> Seq.filter isAppendRegister
            |> Seq.fold (fun map (name : RegisterName) ->
                match name.Char with
                | None -> map
                | Some c ->
                    let c = CharUtil.ToLower c 
                    match NamedRegister.OfChar c |> Option.map RegisterName.Named with
                    | None -> map
                    | Some backingRegisterName ->
                        let backingRegister = Map.find backingRegisterName map
                        let value = AppendRegisterValueBacking(backingRegister)
                        let register = Register(name, value)
                        Map.add name register map) originalMap

        RegisterMap(map)

    member x.GetRegister name = Map.find name _map

    /// Updates the given register with the specified value.  This will also update 
    /// other registers based on the type of update that is being performed.  See 
    /// :help registers for the full details
    member x.SetRegisterValue (reg : Register) regOperation (value : RegisterValue) = 
        if reg.Name <> RegisterName.Blackhole then

            reg.RegisterValue <- value

            let hasNewLine = 
                match value.StringData with 
                | StringData.Block col -> Seq.exists EditUtil.HasNewLine col 
                | StringData.Simple str -> EditUtil.HasNewLine str

            // If this is not the unnamed register then the unnamed register needs to 
            // be updated 
            if reg.Name <> RegisterName.Unnamed then
                let unnamedReg = x.GetRegister RegisterName.Unnamed
                unnamedReg.RegisterValue <- value

            // Update the numbered register based on the type of the operation
            match regOperation with
            | RegisterOperation.Delete ->

                if hasNewLine then

                  // Update the numbered registers with the new values if this delete spanned more
                  // than a single line.  First shift the existing values up the stack
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
                  let regOne = x.GetRegister (RegisterName.Numbered NumberedRegister.Number1)
                  regOne.RegisterValue <- value
            | RegisterOperation.Yank ->

                // If the yank occurs to the unnamed register then update register 0 with the 
                // value
                if reg.Name = RegisterName.Unnamed then
                    let regZero = x.GetRegister (RegisterName.Numbered NumberedRegister.Number0)
                    regZero.RegisterValue <- value
    
            // Possibly update the small delete register
            if reg.Name = RegisterName.Unnamed && regOperation = RegisterOperation.Delete && not hasNewLine then
                let regSmallDelete = x.GetRegister RegisterName.SmallDelete
                regSmallDelete.RegisterValue <- value

    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue register operation value = x.SetRegisterValue register operation value

