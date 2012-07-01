#light
namespace Vim
open System
open System.Diagnostics

[<AbstractClass>]
type VimTrace() =

    static let _traceSwitch = TraceSwitch("VsVim", "VsVim Trace")

    static member TraceSwitch = _traceSwitch

    [<Conditional("TRACE")>]
    static member TraceInfo (msg : string) = 
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, "VsVim " + msg)

    [<Conditional("TRACE")>]
    static member TraceInfo (format : string, [<ParamArrayAttribute>] args : obj []) = 
        let msg = String.Format(format, args)
        let msg = "VsVim " + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)

