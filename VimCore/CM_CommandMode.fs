#light

namespace VimCore.Modes.Command
open VimCore
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions
open VimCore.RegexUtil

type CommandModeData = {
    VimBufferData : IVimBufferData;
    RunFunc : CommandModeData -> KeyInput -> (CommandModeData * option<ModeKind>)
}
    
module CommandModeUtil = 
    
    // Actually process the completed command
    let ProcessCommand (cmd:string) (d:IVimBufferData) = 
        let host = d.VimHost
        match cmd with
            | Match2 "^e\s(.*)$" (_,file) -> host.OpenFile file
            | Match2 "^(\d+)$" (_,lineNum) -> 
                let num = System.Int32.Parse(lineNum)
                let num = TssUtil.VimLineToTssLine(num)
                let tss = d.TextSnapshot
                match num < tss.LineCount with
                    | true -> 
                        let line = tss.GetLineFromLineNumber(num)
                        ViewUtil.MoveToLineStart d.TextView line |> ignore
                    | false -> 
                        host.UpdateStatus("Invalid line number")
            | Match1 "^\$$" _ -> 
                ViewUtil.MoveToLastLineStart d.TextView |> ignore
            | _ -> host.UpdateStatus("Cannot run \"" + cmd)
    
                
  type CommandMode( _data : IVimBufferData ) = 
    let mutable _command : System.String = System.String.Empty
    interface IMode with 
        member x.Commands = Seq.empty
        member x.ModeKind = ModeKind.Command
        member x.CanProcess ki = true
        member x.Process ki = 
            match ki.Key with 
                | Key.Enter ->
                    CommandModeUtil.ProcessCommand _command _data
                    SwitchMode ModeKind.Normal
                | Key.Escape ->
                    SwitchMode ModeKind.Normal
                | _ -> 
                    let c = ki.Char
                    _command <-_command + (c.ToString())
                    _data.VimHost.UpdateStatus(":" + _command)
                    Processed
                    
        member x.OnEnter () =
            _command <- System.String.Empty
        member x.OnLeave () = ()


