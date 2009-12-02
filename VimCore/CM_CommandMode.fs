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
            | Match2 "^e\s(.*)$" (_,file) -> Util.EditFile host file
            | Match2 "^(\d+)$" (_,lineNum) -> Util.JumpToLineNumber d lineNum
            | Match1 "^\$$" _ -> Util.JumpToLastLine d
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


