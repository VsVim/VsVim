#light
namespace Vim
open System
open System.Diagnostics

[<AbstractClass>]
type VimTrace() =

    static let _traceSwitch = TraceSwitch("VsVim", "VsVim Trace")

    static member TraceSwitch = _traceSwitch

    [<Conditional("TRACE")>]
    static member TraceInfo(msg : string) = 
        VimTrace.TraceInfo(msg, null)

    [<Conditional("TRACE")>]
    static member TraceInfo(format : string, [<ParamArrayAttribute>] args : obj []) = 
        let msg = String.Format(format, args)
        let msg = "VsVim " + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)

    [<Conditional("TRACE")>]
    static member TraceError(ex : Exception) = 
        let msg = ex.Message + Environment.NewLine + ex.StackTrace
        VimTrace.TraceError(msg)

    [<Conditional("TRACE")>]
    static member TraceError(msg : string) = 
        VimTrace.TraceError(msg, null)

    [<Conditional("TRACE")>]
    static member TraceError(format : string, [<ParamArrayAttribute>] args : obj []) = 
        let msg = String.Format(format, args)
        let msg = "VsVim Error " + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceError, msg)


