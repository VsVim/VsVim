#light

namespace Vim

type internal RegisterMap (_map: Map<RegisterName,Register> ) =
    new() = 
        let map = 
            RegisterNameUtil.RegisterNames
            |> Seq.map (fun n -> n,Register(n, DefaultRegisterValueBacking() :> IRegisterValueBacking ))
            |> Map.ofSeq
        RegisterMap(map)

    member x.GetRegister name = Map.find name _map
    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.DefaultRegister = x.GetRegister RegisterName.Unnamed
            
          

