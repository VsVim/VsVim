#light

namespace Vim

module internal RegisterUtil =
    let DefaultName = '_'
    let RegisterNames = 
        ['a'..'z'] 
            |> Seq.append ['A'..'Z']
            |> Seq.append ['0'..'9'] 
            |> Seq.append [DefaultName]
    let IsRegisterName c =
        not (RegisterNames |> Seq.choose ( fun i -> match c = i with | true -> Some c | false -> None) |> Seq.isEmpty)

    let BuildRegisterMap = 
        RegisterNames
            |> Seq.map (fun x -> x,(new Register(x)) )
            |> Map.ofSeq

    
type internal RegisterMap (_map: Map<char,Register> ) =
    new() = RegisterMap( RegisterUtil.BuildRegisterMap )
    interface IRegisterMap with
        member x.DefaultRegisterName = RegisterUtil.DefaultName
        member x.RegisterNames = RegisterUtil.RegisterNames
        member x.IsRegisterName c = RegisterUtil.IsRegisterName c
        member x.GetRegister c = Map.find c _map
        member x.DefaultRegister = Map.find (RegisterUtil.DefaultName) _map
            
          

