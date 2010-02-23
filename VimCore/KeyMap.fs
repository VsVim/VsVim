#light

namespace Vim

type internal RemapModeMap = Map<KeyInput, (KeyInput seq * bool)>

type internal KeyMap() =
    
    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    member x.GetKeyMapping ki mode = 
        match _map |> Map.tryFind mode with
        | None -> Seq.singleton ki
        | Some(modeMap) ->  
            match modeMap |> Map.tryFind ki with
            | None -> Seq.singleton ki
            | Some(kiSeq,_) -> kiSeq

    member x.MapWithNoRemap (lhs:string) (rhs:string) (mode:KeyRemapMode) = 
        if StringUtil.Length lhs <> 1 || StringUtil.Length rhs <> 1 then 
            false
        else
            let lhs = lhs.Chars(0)
            let rhs = rhs.Chars(0)
            if System.Char.IsLetterOrDigit(lhs) && System.Char.IsLetterOrDigit(rhs) then
                let leftKi = InputUtil.CharToKeyInput lhs
                let rightKi = InputUtil.CharToKeyInput rhs
                let value = (rightKi,mode)
                let modeMap = 
                    match _map |> Map.tryFind mode with
                    | None -> Map.empty
                    | Some(modeMap) -> modeMap
                let modeMap = Map.add leftKi ((Seq.singleton rightKi),false) modeMap
                _map <- Map.add mode modeMap _map
                true
            else
                false

    interface IKeyMap with
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
